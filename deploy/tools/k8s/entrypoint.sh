#!/bin/sh
# When the cluster exists, join its Docker network and use its internal API address,
# because the address kind publishes on the host is not reachable from this container.
cluster=identity-platform

if kind get clusters 2>/dev/null | grep -qx "$cluster"; then
  docker network connect kind "$(hostname)" 2>/dev/null || true
  kind export kubeconfig --internal --name "$cluster" >/dev/null 2>&1
fi

exec "$@"
