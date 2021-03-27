FROM [[BASE_IMAGE]]

ARG USER_ID
ARG GROUP_ID

RUN if getent passwd ${USER_ID} ; then deluser $(getent passwd ${USER_ID} | cut -d: -f1); fi &&\
    if getent group ${GROUP_ID} ; then delgroup ${GROUP_ID}; fi &&\
    addgroup local --gid ${GROUP_ID} &&\
    adduser --disabled-password --gecos '' -u ${USER_ID} -G $(getent group ${GROUP_ID} | cut -d: -f1) local

USER local