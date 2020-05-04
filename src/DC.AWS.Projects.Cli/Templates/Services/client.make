SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

PROJECT_NAME ?=

CLIENT_NAME = [[CLIENT_NAME]]
PORT = [[PORT]]
CLIENT_PATH = [[CLIENT_PATH]]
CONTAINER_NAME = '$(PROJECT_NAME)-client-$(CLIENT_NAME)'

.PHONY: start
start: stop
	docker run --name $(CONTAINER_NAME) -d \
		-v "$(CURDIR)/$(CLIENT_PATH):/usr/src/app" \
		-p $(PORT):3000 \
		$(PROJECT_NAME)/node-client run dev --hostname 0.0.0.0

.PHONY: stop
stop:
	docker stop $(CONTAINER_NAME) || true
	docker container rm $(CONTAINER_NAME) || true
	
.PHONY: logs
logs:
	docker logs $(CONTAINER_NAME)