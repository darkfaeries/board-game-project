#!/bin/sh
set -eu
# This existing protected route must reject an anonymous request with HTTP 401.
# The server starts accepting requests only after startup migrations finish.
status=$(curl --silent --show-error --max-time 3 --output /dev/null \
  --write-out '%{http_code}' http://127.0.0.1:8080/api/sessions)
[ "$status" = 401 ]
