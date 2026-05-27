#!/bin/bash

set -e

AWS_ENDPOINT="http://floci:4566"
REGION="eu-west-2"

TOPIC_NAME="trade-gateway-traces-updates.fifo"
QUEUE_NAME="trade-gateway-traces-update.fifo"
DLQUEUE_NAME="trade-gateway-traces-update-deadletter.fifo"

echo "Creating SNS FIFO topic..."
TOPIC_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sns create-topic \
  --name "$TOPIC_NAME" \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

echo "Topic ARN: $TOPIC_ARN"

echo "Creating SQS FIFO queue..."
QUEUE_URL=$(aws --endpoint-url=$AWS_ENDPOINT sqs create-queue \
  --queue-name "$QUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

DLQUEUE_URL=$(aws --endpoint-url=$AWS_ENDPOINT sqs create-queue \
  --queue-name "$DLQUEUE_NAME" \
  --attributes FifoQueue=true,ContentBasedDeduplication=true \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

echo "Queue URL: $QUEUE_URL"

QUEUE_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Queue ARN: $QUEUE_ARN"

echo "Applying SQS policy to allow SNS publishing..."



echo "Subscribing queue to topic..."

aws --endpoint-url=$AWS_ENDPOINT sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$QUEUE_ARN" \
  --region $REGION

echo "Done."


# Create Redrive Policy
aws --endpoint-url=$AWS_ENDPOINT sqs set-queue-attributes --queue-url $QUEUE_URL --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"${QUEUE_URL}\",\"maxReceiveCount\":\"1\"}"}'


function is_ready() {
    aws --endpoint-url=$AWS_ENDPOINT sns list-topics --query "Topics[?ends_with(TopicArn, ':${TOPIC_NAME}')].TopicArn" || return 1
    aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-url --queue-name ${QUEUE_NAME} || return 1
    aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-url --queue-name ${DLQUEUE_NAME} || return 1
    return 0
}

while ! is_ready; do
    echo "Waiting until ready"
    sleep 1
done

touch /tmp/ready