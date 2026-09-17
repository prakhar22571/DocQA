FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DocQA.csproj .
RUN dotnet restore DocQA.csproj
COPY . .
RUN dotnet publish DocQA.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV PORT=8080
EXPOSE 8080
CMD ["sh", "-c", "dotnet DocQA.dll --urls http://+:${PORT}"]
