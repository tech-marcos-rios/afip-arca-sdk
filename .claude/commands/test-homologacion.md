---
name: test-homologacion
description: Corre los tests contra el ambiente de homologación de AFIP. Usar con /test-homologacion
---

Ejecutá los tests del proyecto apuntando al ambiente de homologación de AFIP.

Pasos:
1. Verificá que `UsarHomologacion: true` esté configurado en las opciones
2. Corré: `dotnet test src/AfipNet.Tests/ --logger "console;verbosity=detailed"`
3. Si hay errores de conectividad, verificá que los endpoints de homologación estén accesibles:
   - WSAA: https://wsaahomo.afip.gov.ar/ws/services/LoginCms
   - WSFE: https://wswhomo.afip.gov.ar/wsfev1/service.asmx
4. Si hay errores de certificado, verificá que el .p12 sea válido y que la contraseña esté en la variable de entorno `Afip__CertificadoPassword`
5. Mostrá el resumen de resultados y cualquier error relevante
