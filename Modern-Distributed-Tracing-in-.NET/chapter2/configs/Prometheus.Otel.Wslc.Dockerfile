FROM prom/prometheus:v2.44.0

COPY prometheus-otel.yml /etc/prometheus/prometheus.yml
