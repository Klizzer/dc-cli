SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
    DC_CLI_COMMAND := .\.tools\dc-aws.exe
else
    DC_CLI_COMMAND := ./.tools/dc-aws
endif

start: stop
	$(DC_CLI_COMMAND) ensure-infra

.PHONY: stop
stop:
	echo "Nothing to stop"

.PHONY: logs
logs:
	echo "No logs for infrastructure"