# Representative source-folder layout

Place real update content beneath the matching folders, then point the builder at the `Sample-Input` root. Empty folders are ignored by the builder.

```text
Sample-Input
├── WinTAK
│   ├── Maps
│   └── Charts
├── VVOD
├── IMOM
│   ├── Parametrics
│   └── Data
├── DISCORT
├── TRAX
└── AKA
```

Additional immediate child folders are displayed as **Unassigned**. Select one in the application and assign a category before building, or add a reusable category and alias in `DeploymentProfiles.xml`.
