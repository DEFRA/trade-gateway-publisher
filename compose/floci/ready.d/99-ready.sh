#!/bin/bash

function is_ready() {
  # Run all checks in parallel and collect exit codes
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_stream_internal.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_updates.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_updates.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_classificationtreenode_stream_internal.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_classificationtreenode_updates.fifo" >/dev/null &

  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_stream_internal_publisher.fifo &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_stream_internal_publisher.fifo &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_classificationtreenode_stream_internal_publisher.fifo &

  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_updates_test.fifo &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_updates_test.fifo &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_classificationtreenode_updates_test.fifo &

  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_classificationtreenode_stream_internal_asb_publisher.fifo &

  wait || return 1
  return 0
}

is_ready