FROM prom/prometheus:v2.44.0

COPY prometheus-dotnet-monitor.yml /etc/prometheus/prometheus.yml
