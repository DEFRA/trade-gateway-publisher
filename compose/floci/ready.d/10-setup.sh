#!/bin/bash

set -e

AWS_ENDPOINT="http://floci:4566"
REGION="eu-west-2"

INTRA_INTERNAL_TOPIC_NAME="trade_gateway_publisher_intra_stream_internal.fifo"
INTRA_TOPIC_NAME="trade_gateway_publisher_intra_updates.fifo"
INTRA_INTERNAL_QUEUE_NAME="trade_gateway_publisher_intra_stream_internal_publisher.fifo"
INTRA_INTERNAL_DLQUEUE_NAME="trade_gateway_publisher_intra_stream_internal_publisher-deadletter.fifo" 

CHED_INTERNAL_TOPIC_NAME="trade_gateway_publisher_ched_stream_internal.fifo"
CHED_TOPIC_NAME="trade_gateway_publisher_ched_updates.fifo"
CHED_INTERNAL_QUEUE_NAME="trade_gateway_publisher_ched_stream_internal_publisher.fifo"
CHED_INTERNAL_DLQUEUE_NAME="trade_gateway_publisher_ched_stream_internal_publisher-deadletter.fifo"

echo "Creating SNS FIFO topic..."
INTRA_TOPIC_ARN=$(awslocal sns create-topic \
  --name "$INTRA_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

echo "Topic ARN: $INTRA_TOPIC_ARN"

INTRA_INTERNAL_TOPIC_ARN=$(awslocal sns create-topic \
  --name "$INTRA_INTERNAL_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

CHED_TOPIC_ARN=$(awslocal sns create-topic \
  --name "$CHED_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

echo "Topic ARN: $CHED_TOPIC_ARN"

CHED_INTERNAL_TOPIC_ARN=$(awslocal sns create-topic \
  --name "$CHED_INTERNAL_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

echo "Topic ARN: $CHED_INTERNAL_TOPIC_ARN"

# Queue used by integration tests to observe messages published to outbound SNS topic
INTRA_TEST_QUEUE_NAME="trade_gateway_publisher_intra_updates_test.fifo"

echo "Creating SQS FIFO queue..."
INTRA_QUEUE_URL=$(awslocal sqs create-queue \
  --queue-name "$INTRA_INTERNAL_QUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

INTRA_DLQUEUE_URL=$(awslocal sqs create-queue \
  --queue-name "$INTRA_INTERNAL_DLQUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

INTRA_DLQUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$INTRA_DLQUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "INTRA Queue URL: $INTRA_QUEUE_URL"

CHED_QUEUE_URL=$(awslocal sqs create-queue \
  --queue-name "$CHED_INTERNAL_QUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

CHED_DLQUEUE_URL=$(awslocal sqs create-queue \
  --queue-name "$CHED_INTERNAL_DLQUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

CHED_DLQUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$CHED_DLQUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Queue URL: $CHED_QUEUE_URL"

INTRA_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$INTRA_QUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Intra Queue ARN: $INTRA_QUEUE_ARN"

CHED_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$CHED_QUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Queue ARN: $CHED_QUEUE_ARN"

echo "Creating Intra test queue..."
INTRA_TEST_QUEUE_URL=$(awslocal sqs create-queue \
  --queue-name "$INTRA_TEST_QUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

echo "Queue URL: $INTRA_TEST_QUEUE_URL"

INTRA_TEST_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$INTRA_TEST_QUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Subscribing test queue: $INTRA_TEST_QUEUE_ARN to topic: $INTRA_TOPIC_ARN"
awslocal sns subscribe \
  --topic-arn "$INTRA_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$INTRA_TEST_QUEUE_ARN" \
  --attributes '{"RawMessageDelivery": "true"}' \
  --region $REGION

echo "Applying SQS policy to allow SNS publishing..."



echo "Subscribing queue: "$INTRA_QUEUE_ARN" to topic: $INTRA_INTERNAL_TOPIC_ARN"

awslocal sns subscribe \
  --topic-arn "$INTRA_INTERNAL_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$INTRA_QUEUE_ARN" \
  --attributes '{"RawMessageDelivery": "true"}' \
  --region $REGION


echo "Subscribing queue: "$CHED_QUEUE_ARN" to topic: $CHED_INTERNAL_TOPIC_ARN"

awslocal sns subscribe \
  --topic-arn "$CHED_INTERNAL_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$CHED_QUEUE_ARN" \
  --attributes '{"RawMessageDelivery": "true"}' \
  --region $REGION

echo "Done."


# Create Redrive Policy
awslocal sqs set-queue-attributes --queue-url $INTRA_QUEUE_URL --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"${INTRA_DLQUEUE_ARN}\",\"maxReceiveCount\":\"1\"}"}'
awslocal sqs set-queue-attributes --queue-url $CHED_QUEUE_URL --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"${CHED_DLQUEUE_ARN}\",\"maxReceiveCount\":\"1\"}"}'

