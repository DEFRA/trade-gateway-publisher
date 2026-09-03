#!/bin/bash
set -e

# commonly used attributes
DEADLETTER_ATTRIBUTES='{"FifoQueue":"true","ContentBasedDeduplication":"true"}'
SUBSCRIPTION_ATTRIBUTES='{"RawMessageDelivery":"true"}'
TOPIC_ATTRIBUTES='{"FifoTopic":"true","ContentBasedDeduplication":"true"}'

# Creates a queue and subcribes it to the given topic, also creates the deadletter queue
create_subscribed_queue(){ queue="" topic=""

  queue="$1" # param 1 is the queue
  topic="$2" # param 2 is the topic

  # expand the parameters required for the command lines
  DEADLETTER_QUEUE_NAME="$queue-deadletter.fifo"
  QUEUE_NAME="$queue.fifo"
  TOPIC_ARN="$topic.fifo"
  QUEUE_ATTRIBUTES="{\"FifoQueue\":\"true\",\"ContentBasedDeduplication\":\"true\",\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"arn:aws:sqs:$AWS_REGION:000000000000:$DEADLETTER_QUEUE_NAME\\\",\\\"maxReceiveCount\\\":\\\"1\\\"}\",\"Policy\":\"{\\\"Version\\\":\\\"2012-10-17\\\",\\\"Statement\\\": [{\\\"Effect\\\":\\\"Allow\\\",\\\"Principal\\\":{\\\"Service\\\":\\\"sns.amazonaws.com\\\"},\\\"Action\\\":\\\"sqs:SendMessage\\\",\\\"Resource\\\":\\\"arn:aws:sqs:$AWS_REGION:000000000000:$QUEUE_NAME\\\",\\\"Condition\\\":{\\\"ArnEquals\\\":{\\\"aws:SourceArn\\\":\\\"arn:aws:sns:$AWS_REGION:000000000000:$TOPIC_ARN\\\"}}}] }\" }"

  printf 'creating deadletter queue: %s\n with attributes: %s\n' "$DEADLETTER_QUEUE_NAME" "$DEADLETTER_ATTRIBUTES"
  awslocal sqs create-queue --queue-name "$DEADLETTER_QUEUE_NAME" --attributes "$DEADLETTER_ATTRIBUTES"

  printf 'creating queue %s\nwith subscription %s\n and attributes %s\n' "$QUEUE_NAME" "$TOPIC_ARN" "$QUEUE_ATTRIBUTES"
  awslocal sqs create-queue --queue-name "$QUEUE_NAME" --attributes "$QUEUE_ATTRIBUTES"

  # create the SNS subscription for the queue
  awslocal sns subscribe --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:$TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "arn:aws:sqs:$AWS_REGION:000000000000:$QUEUE_NAME" \
    --attributes "$SUBSCRIPTION_ATTRIBUTES"
}

# trade-gateway-publisher topics
awslocal sns create-topic --name trade_gateway_publisher_ched_stream_internal.fifo --attributes "$TOPIC_ATTRIBUTES"
awslocal sns create-topic --name trade_gateway_publisher_ched_updates.fifo --attributes "$TOPIC_ATTRIBUTES"
awslocal sns create-topic --name trade_gateway_publisher_intra_stream_internal.fifo --attributes "$TOPIC_ATTRIBUTES"
awslocal sns create-topic --name trade_gateway_publisher_intra_updates.fifo --attributes "$TOPIC_ATTRIBUTES"

# SNS Queues (use create_subscribed_queue for each pair)
create_subscribed_queue trade_gateway_publisher_ched_stream_internal_publisher trade_gateway_publisher_ched_stream_internal
create_subscribed_queue trade_gateway_publisher_intra_stream_internal_publisher trade_gateway_publisher_intra_stream_internal

# SNS for Azure service bus Queues
create_subscribed_queue trade_gateway_publisher_ched_stream_internal_asb_publisher trade_gateway_publisher_ched_updates
create_subscribed_queue trade_gateway_publisher_intra_stream_internal_asb_publisher trade_gateway_publisher_intra_updates

# test queues
create_subscribed_queue trade_gateway_publisher_intra_updates_test trade_gateway_publisher_intra_updates
create_subscribed_queue trade_gateway_publisher_ched_updates_test trade_gateway_publisher_ched_updates