---
name: publish
description: Empaqueta y publica AfipNet en NuGet.org. Usar con /publish
---

Publicá el paquete AfipNet en NuGet.org. Seguí estos pasos en orden:

1. **Verificar versión**: leé `<Version>` en `src/AfipNet/AfipNet.csproj` y confirmá con el usuario que el número es correcto para esta release.

2. **Correr tests**: `dotnet test` — no publicar si hay tests fallando.

3. **Build en Release**: `dotnet build -c Release`

4. **Empaquetar**: `dotnet pack src/AfipNet/AfipNet.csproj -c Release -o ./nupkg`

5. **Verificar el paquete**: listá el contenido de `./nupkg/` y confirmá que el .nupkg existe.

6. **Publicar** (pedí confirmación antes de ejecutar este paso):
   ```
   dotnet nuget push ./nupkg/AfipNet.*.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json
   ```
   La API key debe estar en la variable de entorno `NUGET_API_KEY`, nunca hardcodeada.

7. **Post-publicación**: actualizá el badge de NuGet en el README con la nueva versión.
