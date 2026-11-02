FROM alpine:3.22

ARG KIND_VERSION=v0.30.0

RUN apk add --no-cache curl docker-cli helm kubectl \
    && arch="$(apk --print-arch | sed -e 's/x86_64/amd64/' -e 's/aarch64/arm64/')" \
    && base="https://github.com/kubernetes-sigs/kind/releases/download/${KIND_VERSION}" \
    && curl -fsSLo /usr/local/bin/kind "${base}/kind-linux-${arch}" \
    && expected="$(curl -fsSL "${base}/kind-linux-${arch}.sha256sum" | cut -d' ' -f1)" \
    && echo "${expected}  /usr/local/bin/kind" | sha256sum -c - \
    && chmod +x /usr/local/bin/kind

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
ENTRYPOINT ["sh", "/usr/local/bin/entrypoint.sh"]
