﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{

    [Collection("CommandProcessor")]
    public class CommandProcessorBulkDepositPostTestsAsync: IDisposable
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly MyCommand _myCommand2 = new MyCommand();
        private readonly Message _message;
        private readonly Message _message2;
        private readonly FakeOutboxSync _fakeOutboxSync;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        public CommandProcessorBulkDepositPostTestsAsync()
        {
            _myCommand.Value = "Hello World";
            _myCommand2.Value = "Update World";

            _fakeOutboxSync = new FakeOutboxSync();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            var topic = "MyCommand";
            _message = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _message2 = new Message(
                new MessageHeader(_myCommand2.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            PolicyRegistry policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy } };
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                messageMapperRegistry,
                _fakeOutboxSync,
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>() {{topic, _fakeMessageProducerWithPublishConfirmation},}));
        }


        [Fact]
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var postedMessageIds = await _commandProcessor.DepositPostAsync(new []{_myCommand, _myCommand2});
            
            //assert
            //message should not be posted
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeFalse();
            
            //message should be in the store
            var depositedPost = _fakeOutboxSync
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message.Id);
            
            //message should be in the store
            var depositedPost2 = _fakeOutboxSync
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message2.Id);

            depositedPost.Should().NotBeNull();
           
            //message should correspond to the command
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
            
            //message should correspond to the command
            depositedPost2.Id.Should().Be(_message2.Id);
            depositedPost2.Body.Value.Should().Be(_message2.Body.Value);
            depositedPost2.Header.Topic.Should().Be(_message2.Header.Topic);
            depositedPost2.Header.MessageType.Should().Be(_message2.Header.MessageType);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
     }
}
