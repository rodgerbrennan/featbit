FROM python:3.9-slim-buster as build
WORKDIR /source

RUN apt update && apt install -y git

RUN pip install poetry==1.7.1

COPY setup.py ./
COPY pyproject.toml poetry.lock ./
RUN touch README.md

COPY app ./app

RUN poetry install
RUN poetry build

RUN poetry export -f requirements.txt -o requirements.txt --without-hashes

RUN pip wheel -w wheelhouse -r requirements.txt

RUN rm pyproject.toml && rm poetry.lock

RUN sed -i 's/ @ git+https:\/\/github.com\/featbit\/infi.clickhouse_orm@ce2a0f249f691eaf2c7841c496987f4e7433d509//' requirements.txt

FROM python:3.9-slim-buster

ARG KAFKA_HOSTS=kafka:9092
ARG CLICKHOUSE_HOST=clickhouse-server
ARG CLICKHOUSE_ALT_HOST=
ARG CLICKHOUSE_PORT=9000
ARG CLICKHOUSE_HTTP_PORT=8123
ARG CLICKHOUSE_KAFKA_HOSTS=kafka:9092
ARG CLICKHOUSE_USER=default
ARG CLICKHOUSE_PASSWORD=
ARG CLICKHOUSE_DATABASE=featbit
ARG CLICKHOUSE_CLUSTER=featbit_ch_cluster
ARG CLICKHOUSE_REPLICATION=true
ARG CLICKHOUSE_SECURE=false
ARG CLICKHOUSE_VERIFY=true
ARG TEST=false
ARG SUFFIX=
ARG MONGO_HOST=mongodb
ARG MONGO_PORT=27017
ARG MONGO_URI=mongodb://admin:password@mongodb:27017
ARG MONGO_INITDB_DATABASE=featbit
ARG IS_PRO=false
ARG CHECK_DB_LIVNESS=true
ENV KAFKA_HOSTS=${KAFKA_HOSTS} \
    CLICKHOUSE_USER=${CLICKHOUSE_USER} \
    CLICKHOUSE_PASSWORD=${CLICKHOUSE_PASSWORD} \
    CLICKHOUSE_HOST=${CLICKHOUSE_HOST} \
    CLICKHOUSE_ALT_HOST=${CLICKHOUSE_ALT_HOST} \
    CLICKHOUSE_PORT=${CLICKHOUSE_PORT} \
    CLICKHOUSE_HTTP_PORT=${CLICKHOUSE_HTTP_PORT} \
    CLICKHOUSE_KAFKA_HOSTS=${CLICKHOUSE_KAFKA_HOSTS} \
    CLICKHOUSE_DATABASE=${CLICKHOUSE_DATABASE} \
    CLICKHOUSE_CLUSTER=${CLICKHOUSE_CLUSTER} \
    CLICKHOUSE_REPLICATION=${CLICKHOUSE_REPLICATION} \
    CLICKHOUSE_SECURE=${CLICKHOUSE_SECURE} \
    CLICKHOUSE_VERIFY=${CLICKHOUSE_VERIFY} \
    CACHE_TYPE=${CACHE_TYPE} \
    TEST=${TEST} \
    SUFFIX=${SUFFIX} \
    MONGO_HOST=${MONGO_HOST} \
    MONGO_PORT=${MONGO_PORT} \
    MONGO_URI=${MONGO_URI} \
    MONGO_DB=${MONGO_INITDB_DATABASE} \
    IS_PRO=${IS_PRO} \
    CHECK_DB_LIVNESS=${CHECK_DB_LIVNESS}

WORKDIR /featbit_da
COPY --from=build /source ./

RUN pip install --no-index --no-deps wheelhouse/*.whl
RUN pip install --editable .
RUN rm -rf wheelhouse

COPY wait-for-it.sh /featbit_da
COPY docker-entrypoint.sh /featbit_da
COPY setup.py /featbit_da

RUN chmod +x docker-entrypoint.sh wait-for-it.sh

ENTRYPOINT ["./docker-entrypoint.sh"]