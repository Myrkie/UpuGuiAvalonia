FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0-aot

RUN apt-get update && apt-get install -y \
    clang \
    lld \
    build-essential \
    zlib1g-dev

WORKDIR /src

COPY UpuGuiAvalonia/ ./UpuGuiAvalonia/

WORKDIR /src/UpuGuiAvalonia

RUN dotnet publish UpuGuiAvalonia.csproj \
    -c Release \
    -r linux-x64 \
    -p:PublishAot=true \
    --self-contained true \
    -o /out
