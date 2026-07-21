FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY src/RcaEngine/RcaEngine.csproj src/RcaEngine/
RUN dotnet restore src/RcaEngine
COPY src/ src/
RUN dotnet publish src/RcaEngine -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /out .
COPY samples/ /bundles/
ENTRYPOINT ["dotnet", "RcaEngine.dll"]
CMD ["/bundles/bundle-memory-ce-storm", "--no-llm"]
