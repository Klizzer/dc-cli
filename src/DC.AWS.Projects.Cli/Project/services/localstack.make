SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
    JJ_EXECUTABLE := ./.tools/jj.exe
else
    JJ_EXECUTABLE := ./.tools/jj
endif

LOCALSTACK_VERSION = latest
LOCALSTACK_SERVICES = 'edge,apigateway,cloudformation,dynamodb,iam,lambda,logs,s3,sts'

PROJECT_NAME ?=

CONTAINER_NAME = '$(PROJECT_NAME)-localstack'

.PHONY: start
start: stop
	mkdir -p ./.localstack
	$(eval LOCALSTACK_API_KEY=`$(JJ_EXECUTABLE) -i ./.settings.json localstackApiKey`)
	docker pull localstack/localstack:$(LOCALSTACK_VERSION)
	docker run --name $(CONTAINER_NAME) -d -p 4563-4599:4563-4599 \
		-p 8055:8080 \
		-v $(CURDIR)/.localstack:/tmp/localstack \
		-v /var/run/docker.sock:/var/run/docker.sock \
		-e SERVICES=$(LOCALSTACK_SERVICES) \
		-e DATA_DIR='/tmp/localstack/data' \
		-e LOCALSTACK_API_KEY='$(LOCALSTACK_API_KEY)' \
		-e LAMBDA_REMOTE_DOCKER=0 \
		-e DEBUG=1 \
		localstack/localstack:$(LOCALSTACK_VERSION)
	dc-aws ensure-localstack -s $(LOCALSTACK_SERVICES)

.PHONY: stop
stop:
	docker stop $(CONTAINER_NAME) || true
	docker container rm $(CONTAINER_NAME) || true
	
.PHONY: logs
logs:
	docker logs $(CONTAINER_NAME)