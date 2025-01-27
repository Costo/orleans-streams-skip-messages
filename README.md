# Reproduction of Streaming issue with Orleans

## Getting started

1. Start the Event Hubs emulator locally using [these instructions](https://learn.microsoft.com/en-us/azure/event-hubs/test-locally-with-event-hub-emulator?tabs=automated-script%2Cusing-kafka)
2. Run the project

Here's what's happening:
- EventProducerTestGrain grain sends messages to a stream backed by an Event Hub
- Consumer grain is subscribed to the stream and receives the messages

## What's wrong?
- When `MetadataMinTimeInCache` is null and the cache has purged all the messages for a stream, then a QueueCacheMissException is raised and **the next batch of message is not delivered**.
