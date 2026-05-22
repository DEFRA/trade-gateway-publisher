#!/bin/bash

set -e

AWS_ENDPOINT="http://localhost:4566"
REGION="eu-west-2"

TOPIC_NAME="my-topic.fifo"
QUEUE_NAME="my-queue.fifo"

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

echo "Queue URL: $QUEUE_URL"

QUEUE_ARN=$(aws --endpoint-url=$AWS_ENDPOINT sqs get-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "Queue ARN: $QUEUE_ARN"

echo "Applying SQS policy to allow SNS publishing..."

POLICY=$(cat <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "Allow-SNS-SendMessage",
      "Effect": "Allow",
      "Principal": "*",
      "Action": "sqs:SendMessage",
      "Resource": "$QUEUE_ARN",
      "Condition": {
        "ArnEquals": {
          "aws:SourceArn": "$TOPIC_ARN"
        }
      }
    }
  ]
}
EOF
)

aws --endpoint-url=$AWS_ENDPOINT sqs set-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attributes Policy="$POLICY" \
  --region $REGION

echo "Subscribing queue to topic..."

aws --endpoint-url=$AWS_ENDPOINT sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$QUEUE_ARN" \
  --region $REGION

echo "Done."