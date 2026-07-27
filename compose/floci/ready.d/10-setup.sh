#!/bin/bash

set -e

# Namespaces to process
namespaces=(
  "trade_gateway_publisher_intra"
  "trade_gateway_publisher_ched"
)

# Test queue for intra updates (kept separate)
INTRA_TEST_QUEUE_NAME="trade_gateway_publisher_intra_updates_test.fifo"

echo "Creating Intra test queue..."
INTRA_TEST_QUEUE_URL=$(awslocal sqs create-queue \
  --queue-name "$INTRA_TEST_QUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $AWS_REGION \
  --query 'QueueUrl' \
  --output text)
INTRA_TEST_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$INTRA_TEST_QUEUE_URL" \
  --attribute-names QueueArn \
  --region $AWS_REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Intra test queue ARN: $INTRA_TEST_QUEUE_ARN"

for ns in "${namespaces[@]}"; do
  echo "\nProcessing namespace: $ns"

  INTERNAL_TOPIC_NAME="${ns}_stream_internal.fifo"
  UPDATES_TOPIC_NAME="${ns}_updates.fifo"
  INTERNAL_QUEUE_NAME="${ns}_stream_internal_publisher.fifo"
  INTERNAL_DLQUEUE_NAME="${ns}_stream_internal_publisher-deadletter.fifo"

  echo "Creating SNS topics: $INTERNAL_TOPIC_NAME and $UPDATES_TOPIC_NAME"
  INTERNAL_TOPIC_ARN=$(awslocal sns create-topic --name "$INTERNAL_TOPIC_NAME" --attributes FifoTopic=true,ContentBasedDeduplication=true --region $AWS_REGION --query 'TopicArn' --output text)
  UPDATES_TOPIC_ARN=$(awslocal sns create-topic --name "$UPDATES_TOPIC_NAME" --attributes FifoTopic=true,ContentBasedDeduplication=true --region $AWS_REGION --query 'TopicArn' --output text)

  echo "Creating SQS queues: $INTERNAL_QUEUE_NAME and $INTERNAL_DLQUEUE_NAME"
  INTERNAL_QUEUE_URL=$(awslocal sqs create-queue --queue-name "$INTERNAL_QUEUE_NAME" --attributes FifoQueue=true,ContentBasedDeduplication=true --region $AWS_REGION --query 'QueueUrl' --output text)
  INTERNAL_DLQUEUE_URL=$(awslocal sqs create-queue --queue-name "$INTERNAL_DLQUEUE_NAME" --attributes FifoQueue=true,ContentBasedDeduplication=true --region $AWS_REGION --query 'QueueUrl' --output text)

  INTERNAL_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url "$INTERNAL_QUEUE_URL" --attribute-names QueueArn --region $AWS_REGION --query 'Attributes.QueueArn' --output text)
  INTERNAL_DLQUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url "$INTERNAL_DLQUEUE_URL" --attribute-names QueueArn --region $AWS_REGION --query 'Attributes.QueueArn' --output text)

  echo "Subscribing internal queue to internal topic"
  awslocal sns subscribe --topic-arn "$INTERNAL_TOPIC_ARN" --protocol sqs --notification-endpoint "$INTERNAL_QUEUE_ARN" --attributes '{"RawMessageDelivery":"true"}' --region $AWS_REGION

  # For the intra namespace, subscribe the test queue to the updates topic
  if [[ "$ns" == *"_intra" ]]; then
    echo "Subscribing test queue to updates topic"
    awslocal sns subscribe --topic-arn "$UPDATES_TOPIC_ARN" --protocol sqs --notification-endpoint "$INTRA_TEST_QUEUE_ARN" --attributes '{"RawMessageDelivery":"true"}' --region $AWS_REGION
  fi

  echo "Applying RedrivePolicy: queue URL=$INTERNAL_QUEUE_URL -> DL ARN=$INTERNAL_DLQUEUE_ARN"
  awslocal sqs set-queue-attributes --queue-url "$INTERNAL_QUEUE_URL" --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$INTERNAL_DLQUEUE_ARN\\\",\\\"maxReceiveCount\\\":\\\"1\\\"}\"}" --region $AWS_REGION

  echo "Namespace $ns done."
done

