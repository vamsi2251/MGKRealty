FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY LayoutManagementAPI/LayoutManagementAPI.csproj LayoutManagementAPI/
RUN dotnet restore LayoutManagementAPI/LayoutManagementAPI.csproj
COPY LayoutManagementAPI/ LayoutManagementAPI/
RUN dotnet publish LayoutManagementAPI/LayoutManagementAPI.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgdiplus tesseract-ocr \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata
EXPOSE 8080
ENTRYPOINT ["dotnet", "LayoutManagementAPI.dll"]
