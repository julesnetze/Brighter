﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Tranformers.AWS;
using Serilog;

namespace GreetingsReceiverConsole
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>

                {
                    var subscriptions = new Subscription[]
                    {
                        new SqsSubscription<GreetingEvent>(
                            new SubscriptionName("paramore.example.greeting"),
                            new ChannelName(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
                            new RoutingKey(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
                            bufferSize: 10,
                            timeoutInMs: 20, 
                            lockTimeout: 30,
                            findTopicBy: TopicFindBy.Convention,
                            makeChannels: OnMissingChannel.Create)
                    };

                    //create the gateway
                    if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
                    {
                        var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);

                        services.AddServiceActivator(options =>
                        {
                            options.Subscriptions = subscriptions;
                            options.ChannelFactory = new ChannelFactory(awsConnection);
                        })
                        .UseInMemoryOutbox()
                        .AutoFromAssemblies();
                        
                        //We need this for the check as to whether an S3 bucket exists
                        services.AddHttpClient();
                
                        //Adds a luggage store based on an S3 bucket
                        //Assume that the sender has already created, but validate it
                        services.AddS3LuggageStore((options) =>
                        {
                            options.Connection = new AWSS3Connection(credentials, RegionEndpoint.EUWest1);
                            options.BucketName = "brightersamplebucketb0561a06-70ec-11ed-a1eb-0242ac120002";
                            options.BucketRegion = S3Region.EUW1;
                            options.StoreCreation = S3LuggageStoreCreation.ValidateExists;
                        });
                    }

                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            await host.RunAsync();




        }
    }
}

