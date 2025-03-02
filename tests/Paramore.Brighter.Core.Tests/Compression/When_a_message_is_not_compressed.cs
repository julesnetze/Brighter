﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class UncompressedPayloadTests
{
    
    [Fact]
    public async Task When_a_message_is_not_gzip_compressed()
    {
        
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip, "application/json");
        
        var smallContent = "small message";
        string mimeType = "application/json";

        var body = new MessageBody(smallContent, mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),body);
        
        //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(smallContent);
    }
    
    [Fact]
    public async Task When_a_message_is_not_zlib_compressed()
    {
        
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Zlib, "application/json");
        
        var smallContent = "small message";
        string mimeType = "application/json";

        var body = new MessageBody(smallContent, mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),body);
        
        //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(smallContent);
    }
    
    [Fact]
    public async Task When_a_message_is_not_brotli_compressed()
    {
        
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Brotli, "application/json");
        
        var smallContent = "small message";
        string mimeType = "application/json";

        var body = new MessageBody(smallContent, mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),body);
        
        //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(smallContent);
    }
}
