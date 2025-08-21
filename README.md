# M'awuGab Agent (Windows Service, .NET 8)

Agent Windows pour la collecte, compression, mise en file transactionnelle et envoi SFTP de journaux .jrn, avec monitoring Prometheus et mise à jour automatique.

## Architecture

- Core: interfaces et modèles (`src/MawuGab/Core/`)
- Infrastructure: file d'attente, ACL, abstractions système (`src/MawuGab/Infrastructure/`)
- Services: collecte, compression, SFTP, mises à jour, métriques (`src/MawuGab/Services/`)
- Hébergement: `Program.cs`, `Worker.cs`

Respect des principes SOLID, DI via `Microsoft.Extensions.DependencyInjection`.

## Configuration

Fichier: `src/MawuGab/appsettings.json`

- Agent.LogSourcePath: dossier source des `.jrn` (pattern `YYYYMMDD.jrn`)
- Agent.QueuePath, Agent.LogsPath: chemins persistant en `C:\ProgramData\MawuGab`
- Agent.ProcessedPath: dossier d'archivage des `.jrn` traités (organisé par `/BANK/GAB/`)
- Sftp: hôte, credentials, chemin distant base `/data`
- Update: URLs manifest et base de téléchargement
- Agent.MetricsPort: port Prometheus (par défaut 9090)

## Build

1. Depuis Windows avec .NET 8 SDK et Visual Studio 2022/`dotnet`:

```powershell
# Publication Release
cd src/MawuGab
 dotnet publish -c Release -r win-x64 --self-contained false
```

Le binaire se trouve sous `src/MawuGab/bin/Release/net8.0-windows/publish/`.

## Installation du Service (sans MSI)

```powershell
# Ouvrir PowerShell en Administrateur
cd scripts
 ./install-service.ps1
```

Pour désinstaller:

```powershell
cd scripts
 ./uninstall-service.ps1
```

## Monitoring

- Endpoint Prometheus: `http://localhost:9090/metrics`
- Compteurs:
  - mawugab_collected_files
  - mawugab_uploaded_files
  - mawugab_failed_queue
  - mawugab_last_update (label version)

## Sécurité

- SFTP via SSH.NET. Activez la vérification d'empreinte (`Sftp.EnableHostKeyVerification = true` et `Fingerprint`).
- Vous pouvez utiliser l'empreinte SHA256 (`Sftp.FingerprintSha256`, format OpenSSH `SHA256:...`) ou la version hexadécimale (`Sftp.Fingerprint`).
- Authentification par clé privée supportée (`Sftp.PrivateKeyPath`, `Sftp.PrivateKeyPassphrase`), sinon mot de passe.
- ACL Windows appliquées sur les dossiers via `AclManager`.
- Pas de stockage de secrets en clair en prod: utilisez le chiffrement DPAPI ou un gestionnaire de secrets d'entreprise.

## Mises à jour

`UpdateManager` télécharge et met en scène les mises à jour. L'application est volontairement conservatrice; l'étape de swap binaire et redémarrage est spécifique à votre politique (SCM, PS scripts, etc.).

## MSI (WiX)

Un squelette de projet WiX est fourni sous `installer/MawuGab.Installer`. Vous devez installer WiX Toolset 3.x et compléter la liste des fichiers copiés (ComponentGroup) pour produire un MSI.

## SLA et Résilience

- Scan et retry toutes les 60s (configurable).
- File d'attente sur disque, reprise automatique.
- Recovery Windows Service configuré pour redémarrer en cas de crash.
- Les fichiers `.jrn` collectés sont déplacés dans `Agent.ProcessedPath` après mise en file, pour éviter une double collecte.

## Avertissements / À compléter

- Pour une conformité PCI-DSS stricte: chiffrement au repos, rotation des clés, durcissement des ACL et journaux, audit.
- Ajoutez la signature de code et la signature MSI.
- Implémentez l'étape d'auto-update atomique selon votre stratégie (side-by-side + restart + rollback).
