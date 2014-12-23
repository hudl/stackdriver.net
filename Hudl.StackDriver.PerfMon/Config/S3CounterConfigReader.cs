using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Timers;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using log4net;
using ServiceStack.Text;
using Timer = System.Timers.Timer;

namespace Hudl.StackDriver.PerfMon.Config
{
    class S3CounterConfigReader : ICounterConfigReader, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(S3CounterConfigReader));
        private readonly string _awsAccessKey;
        private readonly string _awsSecretKey;

        private readonly int _updateSeconds;
        private readonly string _s3Bucket;
        private readonly string _s3Key;

        private readonly Timer _updateTimer;
        private const int MillisecondsPerSecond = 1000;

        private bool _isUpdating = false;

        public DateTime? DataLastModified { get; set; }

        private Timer InitializeTimer(int updateSeconds)
        {
            var timer = new Timer(updateSeconds * MillisecondsPerSecond);
            timer.Elapsed += OnTimerElapsed;
            timer.Enabled = true;
            return timer;
        }

        private S3CounterConfigReader(string s3Bucket, string s3Key)
        {
            Config = null;
            _s3Bucket = s3Bucket;
            _s3Key = s3Key;
        }

        public S3CounterConfigReader(int updateSeconds, string s3Bucket, string s3Key)
            : this(s3Bucket, s3Key)
        {
            _updateSeconds = updateSeconds;

            _updateTimer = InitializeTimer(updateSeconds);
        }

        public S3CounterConfigReader(int updateSeconds, string s3Bucket, string s3Key, string awsAccessKey, string awsSecretKey)
            : this(s3Bucket, s3Key)
        {
            _updateSeconds = updateSeconds;
            _awsAccessKey = awsAccessKey;
            _awsSecretKey = awsSecretKey;
            _updateTimer = InitializeTimer(updateSeconds);
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isUpdating) return;

            try
            {
                _isUpdating = true;
                UpdateConfig();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateConfig()
        {
            try
            {
                using (var client = HasAwsCredentials()
                    ? new AmazonS3Client(_awsAccessKey, _awsSecretKey, RegionEndpoint.USEast1)
                    : new AmazonS3Client(RegionEndpoint.USEast1))
                {
                    var getObjectMetadataRequest = new GetObjectMetadataRequest { BucketName = _s3Bucket, Key = _s3Key };

                    var cancelToken = new CancellationToken();

                    var getObjectMetadataResponse = client.GetObjectMetadataAsync(getObjectMetadataRequest, cancelToken);

                    var fileLastModified = getObjectMetadataResponse.Result.LastModified;

                    // Check if the file has been modified
                    if (!ModifiedSinceLastUpdate(fileLastModified)) return;

                    string fileContents;
                    var objectRequestCancelToken = new CancellationToken();
                    var getObjectRequest = new GetObjectRequest { BucketName = _s3Bucket, Key = _s3Key };

                    // Read the S3 file to a string 
                    try
                    {
                        using (var getObjectResponse = client.GetObjectAsync(getObjectRequest, objectRequestCancelToken))
                        using (var responseStream = getObjectResponse.Result.ResponseStream)
                        using (var reader = new StreamReader(responseStream))
                        {
                            fileContents = reader.ReadToEnd();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Format("Unable to read s3 file {0} {1}", _s3Bucket, _s3Key), ex);
                        return;
                    }
                    // De-serialize the string
                    try
                    {
                        var newConfig = new JsonSerializer<CountersConfig>().DeserializeFromString(fileContents);
                        if (newConfig != null && newConfig.Counters != null)
                        {
                            ConfigUpdated(this, new CounterConfigEventArgs(newConfig.Counters));
                        }
                        DataLastModified = fileLastModified;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Unable load new counter config", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unable to update config", ex);
            }
        }

        private bool ModifiedSinceLastUpdate(DateTime fileLastModified)
        {
            return !DataLastModified.HasValue || DataLastModified.Value < fileLastModified;
        }

        private bool HasAwsCredentials()
        {
            return !string.IsNullOrWhiteSpace(_awsAccessKey) && !string.IsNullOrWhiteSpace(_awsSecretKey);
        }

        public CountersConfig Config { get; private set; }
        public event EventHandler<CounterConfigEventArgs> ConfigUpdated;

        public void TriggerUpdate()
        {
            UpdateConfig();
        }

        public void Dispose()
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();
        }
    }
}
