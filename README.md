# LittleOCR

> Une visionneuse d'images Windows avec reconnaissance de texte (OCR) **100 % hors ligne**,  
> inspirée de l'expérience Photos d'Apple — sans cloud, sans inscription, sans télémétrie.

![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D4)
![Framework](https://img.shields.io/badge/.NET-8.0-512BD4)
![UI](https://img.shields.io/badge/UI-WPF-68217A)
![License](https://img.shields.io/badge/License-WTFPL-brightgreen)

---

## Fonctionnalités

| Fonctionnalité | Détail |
|---|---|
| **Ouverture d'image** | Menu Ouvrir, glisser-déposer — PNG · JPG · BMP · WebP |
| **Visionneuse** | Affichage avec conservation du ratio, scroll, fond neutre sombre |
| **Bouton OCR flottant** | Bas-droit, états visuels : idle / chargement / succès / erreur |
| **OCR hors ligne** | Windows.Media.Ocr (API native Windows 10+) — aucune dépendance externe |
| **Overlay de texte** | Rectangles semi-transparents sur l'image, coordonnées réelles |
| **Sélection** | Clic simple pour sélectionner un bloc, Ctrl+Clic pour multi-sélection |
| **Copie** | Copier tout · Copier la sélection · Masquer/Afficher l'overlay |
| **Gestion des erreurs** | Messages clairs dans la barre de statut |
| **Thème** | Sombre natif, Segoe UI Variable, DPI-aware |

---

## Stack technique

| Composant | Choix | Justification |
|---|---|---|
| **Langage** | C# 12 / .NET 8 | Ecosystème Windows mature, performances, tooling VS 2022 |
| **UI** | WPF | Canvas overlay, MVVM, styles custom, build simple (pas de MSIX requis) |
| **OCR** | `Windows.Media.Ocr` | Natif Windows 10+, offline, bounding boxes par mot, FR/EN/… |
| **Architecture** | MVVM + code-behind canvas | Séparation propre ; le canvas est un détail de vue |

> **Pourquoi WPF plutôt que WinUI 3 ?**  
> WinUI 3 offre une API plus moderne mais impose le packaging MSIX, complexifie le build  
> en debug et n'apporte pas de bénéfice OCR. WPF .NET 8 donne un résultat natif propre  
> avec zero friction à l'installation.

---

## Prérequis

| Prérequis | Version |
|---|---|
| Windows | 10 build 19041 (mai 2020) ou supérieur |
| .NET SDK | 8.0+ |
| Visual Studio | 2022 (workload **.NET Desktop Development**) |
| Pack de langue OCR | Français (ou anglais) installé dans *Paramètres › Heure et langue › Langue* |

### Vérifier les langues OCR disponibles (PowerShell)

```powershell
[Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages |
    Select-Object DisplayName, LanguageTag
```

Si la liste est vide, ajoutez le **pack langue Français** depuis les paramètres Windows.

---

## Build & Run

### Via Visual Studio 2022

1. Ouvrez `LittleOCR.sln`
2. Sélectionnez la configuration **Debug | x64**
3. Appuyez sur **F5** (ou Ctrl+F5 pour lancer sans débogage)

### Via CLI

```bash
cd LittleOCR
dotnet run --project LittleOCR/LittleOCR.csproj -r win-x64
```

### Build Release

```bash
dotnet publish LittleOCR/LittleOCR.csproj \
    -c Release -r win-x64 \
    --self-contained false \
    -o ./publish
```

---

## Structure du projet

```
LittleOCR/
├── LittleOCR.sln
├── LittleOCR/
│   ├── LittleOCR.csproj          – projet WPF .NET 8, cible net8.0-windows10.0.19041.0
│   ├── app.manifest              – DPI awareness PerMonitorV2
│   ├── App.xaml / App.xaml.cs   – point d'entrée, dictionnaire de ressources global
│   ├── Assets/
│   │   └── app.ico               – icône de l'application
│   ├── Models/
│   │   └── OcrResult.cs          – OcrRect · OcrWord · OcrLine · OcrResult
│   ├── Services/
│   │   └── WindowsOcrService.cs  – wrapper Windows.Media.Ocr, chargement bitmap
│   ├── ViewModels/
│   │   ├── RelayCommand.cs       – ICommand générique
│   │   └── MainViewModel.cs      – état, commandes, items overlay
│   ├── Converters/
│   │   └── Converters.cs         – BoolToVisibility, InverseBool, InverseBoolToVis
│   ├── Styles/
│   │   └── AppStyles.xaml        – palette, boutons, scrollbar (thème sombre)
│   └── Views/
│       ├── MainWindow.xaml       – layout XAML complet
│       └── MainWindow.xaml.cs    – canvas overlay, drag & drop, transformations
├── LICENSE                       – WTFPL
└── .gitignore
```

---

## Utilisation

1. **Ouvrir une image** — bouton *Ouvrir* ou glissez-déposez un fichier dans la fenêtre.
2. **Lancer l'OCR** — bouton flottant **⌕ Lire le texte** (bas-droit de l'image).
3. **Interagir avec les résultats** :
   - Les blocs de texte apparaissent encadrés en bleu sur l'image.
   - **Clic simple** sur un bloc → le sélectionne (orange).
   - **Ctrl + Clic** → multi-sélection.
   - **Clic sur le fond** → désélectionne tout.
4. **Barre d'actions** (bas-centre) :
   - *Copier tout* — copie l'intégralité du texte reconnu.
   - *Copier la sélection* — copie uniquement les blocs sélectionnés.
   - *Masquer/Afficher l'OCR* — toggle de l'overlay.

---

## Limitations connues

- **Pack de langue requis** : si aucune langue OCR n'est installée sur Windows,  
  l'application affiche un message d'erreur explicatif.
- **WebP** : supporté si le codec Windows WebP est installé (Windows 11 + certains Windows 10).
- **Grandes images** : l'OCR peut prendre quelques secondes sur des images très haute résolution  
  (> 4K) car le décodage est fait en mémoire.
- **Langues mixtes** : un seul moteur (FR → EN → défaut système) est utilisé par analyse.  
  Si l'image contient plusieurs langues, le taux de reconnaissance peut varier.
- **Rotation automatique** : les images mal orientées (EXIF) ne sont pas auto-rotées.

---

## Pistes d'amélioration

- [ ] Sélection par glisser-déposer sur plusieurs blocs (lasso)
- [ ] Zoom sur l'image (Ctrl+Molette)
- [ ] Choix de la langue OCR dans la barre d'outils
- [ ] Export du texte reconnu en fichier `.txt`
- [ ] Historique des images récentes
- [ ] Indicateur de confiance par mot (score OCR)
- [ ] Icône d'application personnalisée

---

## Licence

[WTFPL](LICENSE) — Do What the Fuck You Want to Public License.