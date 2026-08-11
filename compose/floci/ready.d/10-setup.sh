#!/bin/bash

set -e

# test-reports
awslocal s3 mb s3://reports

# trade-gateway
awslocal sns create-topic --name trade_gateway_ched_updates
awslocal sns create-topic --name trade_gateway_docom_updates
awslocal sns create-topic --name trade_gateway_intra_updates

# trade-gateway-publisher topics
awslocal sns create-topic --name trade_gateway_publisher_ched_stream_internal.fifo --attributes '{"FifoTopic":"true","ContentBasedDeduplication":"true"}'
awslocal sns create-topic --name trade_gateway_publisher_ched_updates.fifo --attributes '{"FifoTopic":"true","ContentBasedDeduplication":"true"}'
awslocal sns create-topic --name trade_gateway_publisher_intra_stream_internal.fifo --attributes '{"FifoTopic":"true","ContentBasedDeduplication":"true"}'
awslocal sns create-topic --name trade_gateway_publisher_intra_updates.fifo --attributes '{"FifoTopic":"true","ContentBasedDeduplication":"true"}'

# trade-gateway-publisher queues
awslocal sqs create-queue --queue-name trade_gateway_publisher_ched_stream_internal_publisher-deadletter.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true"}'
awslocal sqs create-queue --queue-name trade_gateway_publisher_ched_stream_internal_publisher.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true","RedrivePolicy":"{\"deadLetterTargetArn\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_ched_stream_internal_publisher-deadletter.fifo\",\"maxReceiveCount\":\"1\"}","Policy":"{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"sns.amazonaws.com\"},\"Action\":\"sqs:SendMessage\",\"Resource\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_ched_stream_internal_publisher.fifo\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"arn:aws:sns:'"$AWS_REGION"':000000000000:trade_gateway_publisher_ched_stream_internal.fifo\"}}}]}" }'
awslocal sqs create-queue --queue-name trade_gateway_publisher_intra_stream_internal_publisher-deadletter.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true"}'
awslocal sqs create-queue --queue-name trade_gateway_publisher_intra_stream_internal_publisher.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true","RedrivePolicy":"{\"deadLetterTargetArn\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_stream_internal_publisher-deadletter.fifo\",\"maxReceiveCount\":\"1\"}","Policy":"{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"sns.amazonaws.com\"},\"Action\":\"sqs:SendMessage\",\"Resource\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_stream_internal_publisher.fifo\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"arn:aws:sns:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_stream_internal.fifo\"}}}]}" }'
awslocal sqs create-queue --queue-name trade_gateway_publisher_intra_updates_test.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true","Policy":"{\"Version\":\"2012-10-17\",\"Statement\": [{\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"sns.amazonaws.com\"},\"Action\":\"sqs:SendMessage\",\"Resource\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_updates_test.fifo\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"arn:aws:sns:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_stream_internal.fifo\"}}}] }" }'

awslocal sns subscribe --topic-arn arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_stream_internal.fifo --protocol sqs --notification-endpoint arn:aws:sqs:$AWS_REGION:000000000000:trade_gateway_publisher_ched_stream_internal_publisher.fifo --attributes '{"RawMessageDelivery":"true"}'
awslocal sns subscribe --topic-arn arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal.fifo --protocol sqs --notification-endpoint arn:aws:sqs:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal_publisher.fifo --attributes '{"RawMessageDelivery":"true"}'
awslocal sns subscribe --topic-arn arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal.fifo --protocol sqs --notification-endpoint arn:aws:sqs:$AWS_REGION:000000000000:trade_gateway_publisher_intra_updates_test.fifo --attributes RawMessageDelivery=true

awslocal sqs create-queue --queue-name trade_gateway_publisher_ched_stream_internal_asb_publisher-deadletter.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true"}'
awslocal sqs create-queue --queue-name trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true","RedrivePolicy":"{\"deadLetterTargetArn\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_ched_stream_internal_asb_publisher-deadletter.fifo\",\"maxReceiveCount\":\"1\"}","Policy":"{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"sns.amazonaws.com\"},\"Action\":\"sqs:SendMessage\",\"Resource\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"arn:aws:sns:'"$AWS_REGION"':000000000000:trade_gateway_publisher_ched_stream_internal.fifo\"}}}]}" }'

awslocal sns subscribe --topic-arn arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_ched_stream_internal.fifo --protocol sqs --notification-endpoint arn:aws:sqs:$AWS_REGION:000000000000:trade_gateway_publisher_ched_stream_internal_asb_publisher.fifo --attributes '{"RawMessageDelivery":"true"}'

awslocal sqs create-queue --queue-name trade_gateway_publisher_intra_stream_internal_asb_publisher-deadletter.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true"}'
awslocal sqs create-queue --queue-name trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo --attributes '{"FifoQueue":"true","ContentBasedDeduplication":"true","RedrivePolicy":"{\"deadLetterTargetArn\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_stream_internal_asb_publisher-deadletter.fifo\",\"maxReceiveCount\":\"1\"}","Policy":"{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"sns.amazonaws.com\"},\"Action\":\"sqs:SendMessage\",\"Resource\":\"arn:aws:sqs:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"arn:aws:sns:'"$AWS_REGION"':000000000000:trade_gateway_publisher_intra_stream_internal.fifo\"}}}]}" }'

awslocal sns subscribe --topic-arn arn:aws:sns:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal.fifo --protocol sqs --notification-endpoint arn:aws:sqs:$AWS_REGION:000000000000:trade_gateway_publisher_intra_stream_internal_asb_publisher.fifo --attributes '{"RawMessageDelivery":"true"}'