SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

PROJECT_NAME ?=

PROXY_NAME = [[PROXY_NAME]]
CONFIG_PATH = [[CONFIG_PATH]]
PORT = [[PORT]]
CONTAINER_NAME = $(PROJECT_NAME)-proxy-$(PROXY_NAME)

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
else
    DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
endif

.PHONY: start
start: stop
ifeq ($(DETECTED_OS), Windows)
	if not exist "$(CURDIR)/$(CONFIG_PATH)/_child_paths" mkdir "$(CURDIR)/$(CONFIG_PATH)/_child_paths"
else
	mkdir -p "$(CURDIR)/$(CONFIG_PATH)/_child_paths"
endif
	docker pull nginx
	docker run --name $(CONTAINER_NAME) -d \
		-v "$(CURDIR)/$(CONFIG_PATH)/proxy.nginx.conf:/etc/nginx/nginx.conf" \
		-v "$(CURDIR)/$(CONFIG_PATH)/_child_paths:/etc/nginx/_child_paths" \
		-v "$(CURDIR)/config/.generated/proxy-upstreams:/etc/nginx/upstreams"
		-p $(PORT):80 nginx

.PHONY: stop
stop:
	docker stop $(CONTAINER_NAME) || true
	docker container rm $(CONTAINER_NAME) || true
	
.PHONY: logs
logs:
	docker logs $(CONTAINER_NAME)