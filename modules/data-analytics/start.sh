#!/bin/bash
set -e

export FLASK_APP='flasky:app'

if [ "$ENABLE_OPENTELEMETRY" = "true" ]; then
    opentelemetry-instrument gunicorn 'flasky:app'
else
    gunicorn 'flasky:app'
fi