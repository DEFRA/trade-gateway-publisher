#!/bin/bash
export AWS_REGION=eu-west-2
export AWS_DEFAULT_REGION=eu-west-2
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

# SNS topics
aws --endpoint-url=http://localhost:4566 sns create-topic --name trade_gateway_publisher_intra_stream_internal.fifo --attributes FifoTopic=true
aws --endpoint-url=http://localhost:4566 sns create-topic --name trade_gateway_publisher_intra_updates.fifo --attributes FifoTopic=true

# SQS queues
aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name trade_gateway_publisher_intra_stream_internal_publisher.fifo --attributes FifoQueue=true
aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name trade_gateway_publisher_intra_updates_publisher.fifo --attributes FifoQueue=true

# Subscribe queues to topics
STREAM_TOPIC_ARN=$(aws --endpoint-url=http://localhost:4566 sns list-topics --query "Topics[?contains(TopicArn, 'trade_gateway_publisher_intra_stream_internal')].TopicArn" --output text)
STREAM_QUEUE_ARN=$(aws --endpoint-url=http://localhost:4566 sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/trade_gateway_publisher_intra_stream_internal_publisher.fifo --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)
aws --endpoint-url=http://localhost:4566 sns subscribe --topic-arn "$STREAM_TOPIC_ARN" --protocol sqs --notification-endpoint "$STREAM_QUEUE_ARN"

UPDATES_TOPIC_ARN=$(aws --endpoint-url=http://localhost:4566 sns list-topics --query "Topics[?contains(TopicArn, 'trade_gateway_publisher_intra_updates')].TopicArn" --output text)
UPDATES_QUEUE_ARN=$(aws --endpoint-url=http://localhost:4566 sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/trade_gateway_publisher_intra_updates_publisher.fifo --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)
aws --endpoint-url=http://localhost:4566 sns subscribe --topic-arn "$UPDATES_TOPIC_ARN" --protocol sqs --notification-endpoint "$UPDATES_QUEUE_ARN"

# Wait until topics and queues are available
echo "Waiting for SNS topics and SQS queues to be available..."
while true; do
  STREAM_TOPIC=$(aws --endpoint-url=http://localhost:4566 sns list-topics --query "Topics[?contains(TopicArn, 'trade_gateway_publisher_intra_stream_internal')].TopicArn" --output text)
  UPDATES_TOPIC=$(aws --endpoint-url=http://localhost:4566 sns list-topics --query "Topics[?contains(TopicArn, 'trade_gateway_publisher_intra_updates')].TopicArn" --output text)
  STREAM_QUEUE=$(aws --endpoint-url=http://localhost:4566 sqs list-queues --queue-name-prefix trade_gateway_publisher_intra_stream_internal_publisher --query 'QueueUrls[0]' --output text)
  UPDATES_QUEUE=$(aws --endpoint-url=http://localhost:4566 sqs list-queues --queue-name-prefix trade_gateway_publisher_intra_updates_publisher --query 'QueueUrls[0]' --output text)

  if [ -n "$STREAM_TOPIC" ] && [ -n "$UPDATES_TOPIC" ] && [ -n "$STREAM_QUEUE" ] && [ -n "$UPDATES_QUEUE" ]; then
    echo "All SNS topics and SQS queues are available."
    break
  fi

  echo "Not all resources available yet, retrying in 2 seconds..."
  sleep 2
done
