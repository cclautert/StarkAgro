# MQTT TLS Certificates

Place the following files here to enable the TLS listener on port 8883:

| File | Description |
|------|-------------|
| `ca.crt` | CA certificate (self-signed or issued by your CA) |
| `server.crt` | Broker server certificate signed by the CA |
| `server.key` | Broker server private key |

**DO NOT commit real certificates or private keys to git.**

## Generating self-signed certs (development / staging)

```bash
# CA key and certificate
openssl genrsa -out ca.key 4096
openssl req -new -x509 -days 3650 -key ca.key -out ca.crt \
  -subj "/CN=StarkAgro MQTT CA"

# Server key and CSR
openssl genrsa -out server.key 2048
openssl req -new -key server.key -out server.csr \
  -subj "/CN=mqtt.agripeweb.com"

# Sign the server certificate with the CA
openssl x509 -req -days 730 -in server.csr -CA ca.crt -CAkey ca.key \
  -CAcreateserial -out server.crt

# Clean up intermediate files
rm ca.key ca.srl server.csr
```

## IoT device (ESP8266 / ESP32)

Flash `ca.crt` onto the device and configure it to connect to port 8883 with TLS.
The device must present the CA certificate to verify the broker identity.

## Worker (internal Docker network)

The worker connects via the internal Docker bridge (`mqtt:1883`).
TLS is optional for Docker-internal connections; set `Mqtt__UseTls=false` in the
worker's environment unless you require end-to-end encryption within the host.
For TLS on the internal connection, set `Mqtt__UseTls=true` and `Mqtt__Port=8883`
and supply `ca.crt` to the worker container.
