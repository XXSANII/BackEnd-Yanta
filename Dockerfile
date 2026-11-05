# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY YantaBackEnd.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish YantaBackEnd.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# แพลตฟอร์มส่งพอร์ตมาทาง $PORT (Render/Cloud Run) ถ้าไม่มีใช้ 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet YantaBackEnd.dll"]
