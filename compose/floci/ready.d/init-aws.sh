#!/bin/bash

set -e

AWS_ENDPOINT="http://floci:4566"
REGION="eu-west-2"

INTRA_INTERNAL_TOPIC_NAME="trade_gateway_intra_stream_internal.fifo"
INTRA_TOPIC_NAME="trade_gateway_intra_updates.fifo"
INTRA_INTERNAL_QUEUE_NAME="trade_gateway_intra_stream_publisher_internal.fifo"
INTRA_INTERNAL_DLQUEUE_NAME="trade_gateway_intra_stream_publisher_internal-deadletter.fifo"

CHED_INTERNAL_TOPIC_NAME="trade_gateway_ched_stream_internal.fifo"
CHED_TOPIC_NAME="trade_gateway_ched_updates.fifo"
CHED_INTERNAL_QUEUE_NAME="trade_gateway_ched_stream_publisher_internal.fifo"
CHED_INTERNAL_DLQUEUE_NAME="trade_gateway_ched_stream_publisher_internal-deadletter.fifo"

echo "Creating SNS FIFO topic..."
INTRA_TOPIC_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sns create-topic \
  --name "$INTRA_INTERNAL_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

echo "Topic ARN: $INTRA_TOPIC_ARN"


INTRA_INTERNAL_TOPIC_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sns create-topic \
  --name "$INTRA_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

CHED_TOPIC_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sns create-topic \
  --name "$CHED_INTERNAL_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

echo "Topic ARN: $CHED_TOPIC_ARN"


CHED_INTERNAL_TOPIC_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sns create-topic \
  --name "$CHED_TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

echo "Topic ARN: $CHED_INTERNAL_TOPIC_ARN"

echo "Creating SQS FIFO queue..."
QUEUE_URL=$(aws --endpoint-url=$AWS_ENDPOINT sqs create-queue \
  --queue-name "$INTRA_INTERNAL_QUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

DLQUEUE_URL=$(aws --endpoint-url=$AWS_ENDPOINT sqs create-queue \
  --queue-name "$INTRA_INTERNAL_DLQUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

echo "Queue URL: $QUEUE_URL"

CHED_QUEUE_URL=$(aws --endpoint-url=$AWS_ENDPOINT sqs create-queue \
  --queue-name "$CHED_INTERNAL_QUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

CHED_DLQUEUE_URL=$(aws --endpoint-url=$AWS_ENDPOINT sqs create-queue \
  --queue-name "$CHED_INTERNAL_DLQUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

echo "Queue URL: $CHED_QUEUE_URL"

QUEUE_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Queue ARN: $QUEUE_ARN"

CHED_QUEUE_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-attributes \
  --queue-url "$CHED_QUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Queue ARN: $CHED_QUEUE_ARN"

echo "Applying SQS policy to allow SNS publishing..."



echo "Subscribing queue: "$QUEUE_ARN" to topic: $INTRA_TOPIC_ARN"

aws --endpoint-url=$AWS_ENDPOINT sns subscribe \
  --topic-arn "$INTRA_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$QUEUE_ARN" \
  --attributes '{"RawMessageDelivery": "true"}' \
  --region $REGION


echo "Subscribing queue: "$CHED_QUEUE_ARN" to topic: $CHED_INTRA_TOPIC_ARN"

aws --endpoint-url=$AWS_ENDPOINT sns subscribe \
  --topic-arn "$CHED_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$CHED_QUEUE_ARN" \
  --attributes '{"RawMessageDelivery": "true"}' \
  --region $REGION

echo "Done."


# Create Redrive Policy
aws --endpoint-url=$AWS_ENDPOINT sqs set-queue-attributes --queue-url $QUEUE_URL --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"${QUEUE_URL}\",\"maxReceiveCount\":\"1\"}"}'
aws --endpoint-url=$AWS_ENDPOINT sqs set-queue-attributes --queue-url $CHED_QUEUE_URL --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"${CHED_QUEUE_URL}\",\"maxReceiveCount\":\"1\"}"}'

function is_ready() {
    aws --endpoint-url=$AWS_ENDPOINT sns list-topics --query "Topics[?ends_with(TopicArn, ':${INTRA_INTERNAL_TOPIC_NAME}')].TopicArn" || return 1
    aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-url --queue-name ${INTRA_INTERNAL_QUEUE_NAME} || return 1
    aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-url --queue-name ${INTRA_INTERNAL_DLQUEUE_NAME} || return 1

    aws --endpoint-url=$AWS_ENDPOINT sns list-topics --query "Topics[?ends_with(TopicArn, ':${CHED_INTERNAL_TOPIC_NAME}')].TopicArn" || return 1
    aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-url --queue-name ${CHED_INTERNAL_QUEUE_NAME} || return 1
    aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-url --queue-name ${CHED_INTERNAL_DLQUEUE_NAME} || return 1
    return 0
}

while ! is_ready; do
    echo "Waiting until ready"
    sleep 1
done

touch /tmp/ready