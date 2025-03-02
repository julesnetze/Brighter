﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.Tranformers.AWS;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Transformers
{
    public class LargeMessagePayloadWrapTests : IDisposable
    {
        private WrapPipeline<MyLargeCommand> _transformPipeline;
        private readonly TransformPipelineBuilder _pipelineBuilder;
        private readonly MyLargeCommand _myCommand;
        private readonly S3LuggageStore _luggageStore;
        private readonly AmazonS3Client _client;
        private readonly string _bucketName;
        private string _id;

        public LargeMessagePayloadWrapTests()
        {
            //arrange
            TransformPipelineBuilder.ClearPipelineCache();

            var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()))
            {
                { typeof(MyLargeCommand), typeof(MyLargeCommandMessageMapper) }
            };

            _myCommand = new MyLargeCommand(6000);

            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();

            _client = new AmazonS3Client(credentials, region);
            AmazonSecurityTokenServiceClient stsClient = new(credentials, region);

            var services = new ServiceCollection();
            services.AddHttpClient();
            var provider = services.BuildServiceProvider();
            IHttpClientFactory httpClientFactory = provider.GetService<IHttpClientFactory>();

            _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
            
            _luggageStore = S3LuggageStore
                .CreateAsync(
                    client: _client,
                    bucketName: _bucketName,
                    storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                    httpClientFactory: httpClientFactory,
                    stsClient: stsClient,
                    bucketRegion: S3Region.EUW1,
                    tags: new List<Tag>() { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                    acl: S3CannedACL.Private,
                    abortFailedUploadsAfterDays: 1,
                    deleteGoodUploadsAfterDays: 1)
                .GetAwaiter()
                .GetResult();

            var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_luggageStore));

            _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        }

        [Fact]
        public async Task When_wrapping_a_large_message()
        {
            //act
            _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
            var message = _transformPipeline.WrapAsync(_myCommand).Result;

            //assert
            message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK).Should().BeTrue();
            _id = (string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
            message.Body.Value.Should().Be($"Claim Check {_id}");
            
            (await _luggageStore.HasClaimAsync(_id)).Should().BeTrue();
        }

        public void Dispose()
        {
            //We have to empty objects from a bucket before deleting it
            _luggageStore.DeleteAsync(_id).GetAwaiter().GetResult();
            _client.DeleteBucketAsync(_bucketName).GetAwaiter().GetResult();
        }
    }
}
