FROM debian:buster
MAINTAINER Thomas Carlier "carlier.thomas@gmail.com"

ENV FS_MAJOR 1.10
ENV FS_VERSION 1.10.2
ENV REFRESHED_AT 2021-02-09

RUN apt update && apt -y --quiet upgrade && apt install -y --quiet gnupg2 wget \
    && wget -O - https://files.freeswitch.org/repo/deb/debian-release/fsstretch-archive-keyring.asc | apt-key add - \
    && echo "deb http://files.freeswitch.org/repo/deb/debian-release/ buster main" > /etc/apt/sources.list.d/freeswitch.list \
    && echo "deb-src http://files.freeswitch.org/repo/deb/debian-release/ buster main" >> /etc/apt/sources.list.d/freeswitch.list \
    && apt update && apt clean && rm -rf /var/lib/apt/lists/*

RUN groupadd -r freeswitch && useradd -r -g freeswitch freeswitch

RUN apt update && apt -y --quiet upgrade && apt install -y freeswitch-meta-all \
    && apt update && apt clean && rm -rf /var/lib/apt/lists/*

CMD ["/usr/bin/freeswitch -nc -nf -nonat"]