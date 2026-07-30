#!/bin/bash
INTRA_INTERNAL_TOPIC_NAME="trade_gateway_publisher_intra_stream_internal.fifo"
INTRA_TOPIC_NAME="trade_gateway_publisher_intra_updates.fifo"
INTRA_INTERNAL_QUEUE_NAME="trade_gateway_publisher_intra_stream_internal_publisher.fifo"
INTRA_INTERNAL_DLQUEUE_NAME="trade_gateway_publisher_intra_stream_internal_publisher-deadletter.fifo" 
INTRA_ASB_INTERNAL_QUEUE_NAME="trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo"
INTRA_ASB_INTERNAL_DLQUEUE_NAME="trade_gateway_publisher_intra_stream_internal_asb_publisher-deadletter.fifo"
  
CHED_INTERNAL_TOPIC_NAME="trade_gateway_publisher_ched_stream_internal.fifo"
CHED_TOPIC_NAME="trade_gateway_publisher_ched_updates.fifo"
CHED_INTERNAL_QUEUE_NAME="trade_gateway_publisher_ched_stream_internal_publisher.fifo"
CHED_INTERNAL_DLQUEUE_NAME="trade_gateway_publisher_ched_stream_internal_publisher-deadletter.fifo"
CHED_ASB_INTERNAL_QUEUE_NAME="trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo"
CHED_ASB_INTERNAL_DLQUEUE_NAME="trade_gateway_publisher_ched_stream_internal_asb_publisher-deadletter.fifo"
  
# Queue used by integration tests to observe messages published to outbound SNS topic
INTRA_TEST_QUEUE_NAME="trade_gateway_publisher_intra_updates_test.fifo"

function is_ready() {
    awslocal sns list-topics --query "Topics[?ends_with(TopicArn, ':${INTRA_INTERNAL_TOPIC_NAME}')].TopicArn" || return 1
    awslocal sqs get-queue-url --queue-name ${INTRA_INTERNAL_QUEUE_NAME} || return 1
    awslocal sqs get-queue-url --queue-name ${INTRA_INTERNAL_DLQUEUE_NAME} || return 1
    awslocal sqs get-queue-url --queue-name ${INTRA_ASB_INTERNAL_QUEUE_NAME} || return 1
    awslocal sqs get-queue-url --queue-name ${INTRA_ASB_INTERNAL_DLQUEUE_NAME} || return 1

    awslocal sns list-topics --query "Topics[?ends_with(TopicArn, ':${CHED_INTERNAL_TOPIC_NAME}')].TopicArn" || return 1
    awslocal sqs get-queue-url --queue-name ${CHED_INTERNAL_QUEUE_NAME} || return 1
    awslocal sqs get-queue-url --queue-name ${CHED_INTERNAL_DLQUEUE_NAME} || return 1
    awslocal sqs get-queue-url --queue-name ${CHED_ASB_INTERNAL_QUEUE_NAME} || return 1
    awslocal sqs get-queue-url --queue-name ${CHED_ASB_INTERNAL_DLQUEUE_NAME} || return 1
	
	awslocal sqs get-queue-url --queue-name ${INTRA_TEST_QUEUE_NAME} || return 1
    return 0
}

while ! is_ready; do
    echo "Waiting until ready"
    sleep 1
done