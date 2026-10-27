#!/bin/bash

function is_ready() {
  # Run checks in small batches to reduce startup contention on CI runners.

  # SNS topics (2 batches of 3)
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_stream_internal.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_updates.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal.fifo" >/dev/null &
  wait || return 1

  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_updates.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_refdata_stream_internal.fifo" >/dev/null &
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_refdata_updates.fifo" >/dev/null &
  wait || return 1

  # SQS queues (3 batches of 3)
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_stream_internal_publisher.fifo >/dev/null &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_stream_internal_publisher.fifo >/dev/null &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_refdata_stream_internal_publisher.fifo >/dev/null &
  wait || return 1

  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_updates_test.fifo >/dev/null &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_updates_test.fifo >/dev/null &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_refdata_updates_test.fifo >/dev/null &
  wait || return 1

  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo >/dev/null &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo >/dev/null &
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_refdata_stream_internal_asb_publisher.fifo >/dev/null &
  wait || return 1

  return 0
}

is_ready