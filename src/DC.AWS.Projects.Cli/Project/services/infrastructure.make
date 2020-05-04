SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

start: stop
	dc-aws ensure-infra

.PHONY: stop
stop:
	echo "Nothing to stop"

.PHONY: logs
logs:
	echo "No logs for infrastructure"