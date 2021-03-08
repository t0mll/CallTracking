# Call Tracking

## Development
This project uses the following tools.
### Kafka
[Kafka Docker Quickstart](https://docs.confluent.io/platform/current/quickstart/ce-docker-quickstart.html)
### FreeSWITCH
[FreeSWITCH Debian 10 Buster](https://freeswitch.org/confluence/display/FREESWITCH/Debian+10+Buster)
### Docker Desktop
[Docker desktop](https://www.docker.com/products/docker-desktop)
### WSL 2
[Windows Subsystem for Linux](https://docs.microsoft.com/en-us/windows/wsl/install-win10)


## Getting Started
Once you have installed the all the tools you should just be able to to start the project using ***Docker Compose***.

#### Docker Compose

##### FreeSWITCH
This container runs the lastest version of FreeSWITCH with vanilla config.
```yml
  freeswitch:
    hostname: freeswitch
    container_name: freeswitch
    image: ${DOCKER_REGISTRY-}freeswitch
    build:
      context: Voice/FreeSWITCH
      dockerfile: Dockerfile
```

##### Consumer Worker
This container runs the Consumer worker fetching the message from Kafka.
```yml
  calltracking.fsconsumerworker:
    image: ${DOCKER_REGISTRY-}calltrackingfsconsumerworker
    container_name: fs-consumer-worker
    build:
      context: .
      dockerfile: CallTracking.FSConsumerWorker/Dockerfile
```

##### Producer Worker
This container runs the Producer worker which is establishing an Inbound Socket connection to the FreeSWITCH container (ESL:8021).
The worker is listening for all message and print Info log for each message received.
Only Channel Answer message are sent to Kafka.
```yml
  calltracking.fsproducerworker:
    image: ${DOCKER_REGISTRY-}calltrackingfsproducerworker
    container_name: fs-producer-worker
    build:
      context: .
      dockerfile: CallTracking.FSProducerWorker/Dockerfile
    depends_on:
      - freeswitch
```

##### Kafka
There are a few containers that are started for Kafka. Use ***control-center*** [http://localhost:9021](http://localhost:9021) to check the messages.
```yml
  zookeeper:
    image: confluentinc/cp-zookeeper:6.1.0
    hostname: zookeeper
    container_name: zookeeper

  broker:
    image: confluentinc/cp-server:6.1.0
    hostname: broker
    container_name: broker
    depends_on:
      - zookeeper

  schema-registry:
    image: confluentinc/cp-schema-registry:6.1.0
    hostname: schema-registry
    container_name: schema-registry
    depends_on:
      - broker

  connect:
    image: cnfldemos/cp-server-connect-datagen:0.4.0-6.1.0
    hostname: connect
    container_name: connect
    depends_on:
      - broker
      - schema-registry

  control-center:
    image: confluentinc/cp-enterprise-control-center:6.1.0
    hostname: control-center
    container_name: control-center
    depends_on:
      - broker
      - schema-registry
      - connect
      - ksqldb-server

  ksqldb-server:
    image: confluentinc/cp-ksqldb-server:6.1.0
    hostname: ksqldb-server
    container_name: ksqldb-server
    depends_on:
      - broker
      - connect

  ksqldb-cli:
    image: confluentinc/cp-ksqldb-cli:6.1.0
    container_name: ksqldb-cli
    depends_on:
      - broker
      - connect
      - ksqldb-server

  ksql-datagen:
    image: confluentinc/ksqldb-examples:6.1.0
    hostname: ksql-datagen
    container_name: ksql-datagen
    depends_on:
      - ksqldb-server
      - broker
      - schema-registry
      - connect

  rest-proxy:
    image: confluentinc/cp-kafka-rest:6.1.0
    depends_on:
      - broker
      - schema-registry
```