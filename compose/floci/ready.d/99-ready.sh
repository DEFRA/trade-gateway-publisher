#!/bin/bash

function is_ready() {

  # trade-gateway-publisher
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_stream_internal.fifo" >/dev/null || return 1
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_updates.fifo" >/dev/null || return 1
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal.fifo" >/dev/null || return 1
  awslocal sns get-topic-attributes --topic-arn "arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_updates.fifo" >/dev/null || return 1
  
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_stream_internal_publisher.fifo || return 1
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_stream_internal_publisher.fifo || return 1
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_updates_test.fifo || return 1

  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo || return 1
  awslocal sqs get-queue-url --queue-name trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo || return 1
  return 0
}

is_ready