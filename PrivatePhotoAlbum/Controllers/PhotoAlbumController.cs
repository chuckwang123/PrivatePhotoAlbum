using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PrivatePhotoAlbum.Models;
using System.Linq;

namespace PrivatePhotoAlbum.Controllers
{
    [Route("api/[controller]")]
    public class PhotoAlbumController : Controller
    {
        private readonly AwsCredential _awsCredential;
        private IAmazonS3 _client;
        public PhotoAlbumController(IOptions<AwsCredential> optionsAccessor)
        {
            _awsCredential = optionsAccessor.Value;
        }



        [HttpGet("[action]")]
        public async Task<IEnumerable<string>> ListPhotos()
        {
            using (_client = new AmazonS3Client(_awsCredential.Key, _awsCredential.Secret ,Amazon.RegionEndpoint.USEast1))
            {
               return await ListingObjects();
            }
        }

        private async Task<IEnumerable<string>> ListingObjects()
        {
            var listOfPhotos = new List<string>();
            ListObjectsV2Response response = null;
            var folders = new List<string>();
            try
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _awsCredential.Bucket
                };
               
                do
                {
                    response = await _client.ListObjectsV2Async(request);

                    // Process response.
                    listOfPhotos.AddRange(response.S3Objects.Select(entry => entry.Key));
                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated == true);
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                    ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Check the provided AWS Credentials.");
                    Console.WriteLine("To sign up for service, go to http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("Error occurred. Message:'{0}' when listing objects", amazonS3Exception.Message);
                }
            }
            if (response != null)
            {
                folders = response.S3Objects.Where(x =>
                    x.Key.EndsWith(@"/") && x.Size == 0).Select(s => s.Key).ToList();
            }
            return listOfPhotos.Except(folders).Select(GetPreSignedUrl);
        }

        private string GetPreSignedUrl(string fileKey)
        {
            using (_client = new AmazonS3Client(_awsCredential.Key, _awsCredential.Secret, Amazon.RegionEndpoint.USEast1))
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _awsCredential.Bucket,
                    Key = fileKey,
                    Protocol = Protocol.HTTPS,
                    Expires = DateTime.Now.AddMinutes(10)
                };

                return _client.GetPreSignedURL(request);
            }
        }
    }
}
