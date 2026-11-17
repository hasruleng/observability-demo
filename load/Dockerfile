FROM eclipse-temurin:21-jre-alpine

ARG JMETER_VERSION=5.6.3
ARG JMETER_SHA512=5978a1a35edb5a7d428e270564ff49d2b1b257a65e17a759d259a9283fc17093e522fe46f474a043864aea6910683486340706d745fcdf3db1505fd71e689083

RUN apk add --no-cache ca-certificates curl tar \
    && curl -fsSLo /tmp/apache-jmeter.tgz "https://archive.apache.org/dist/jmeter/binaries/apache-jmeter-${JMETER_VERSION}.tgz" \
    && echo "${JMETER_SHA512}  /tmp/apache-jmeter.tgz" | sha512sum -c - \
    && tar -xzf /tmp/apache-jmeter.tgz -C /opt \
    && rm /tmp/apache-jmeter.tgz

ENV JMETER_HOME=/opt/apache-jmeter-5.6.3
ENV PATH="${JMETER_HOME}/bin:${PATH}"
ENV HEAP="-Xms128m -Xmx384m"
WORKDIR /test
ENTRYPOINT ["jmeter"]
