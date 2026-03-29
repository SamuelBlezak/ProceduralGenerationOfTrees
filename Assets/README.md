# Procedurálne Generovanie Modelov Stromov
## Bakalárska práca — Unity C# implementácia

### Prehľad projektu

Tento projekt implementuje procedurálny generátor stromov v Unity pomocou L-systémov
(Lindenmayerových systémov). Inšpirovaný prácou "Interactive Invigoration: Volumetric
Modeling of Trees with Strands" (Li et al., 2024), prispôsobený na bakalársku úroveň.

---

### Štruktúra projektu

```
ProceduralTree/
├── Scripts/
│   ├── Core/
│   │   ├── LSystem.cs            # L-systém prepísavací engine
│   │   ├── TurtleInterpreter.cs  # 3D turtle → TreeSkeleton
│   │   └── MeshBuilder.cs        # TreeSkeleton → Unity Mesh
│   ├── Data/
│   │   ├── TreePreset.cs         # ScriptableObject s parametrami
│   │   └── TreeSkeleton.cs       # Dátové štruktúry kostry
│   ├── Editor/
│   │   └── TreeGeneratorEditor.cs # Vlastný Unity inspektor
│   ├── Utils/
│   │   └── TreeSpecies.cs        # Predefinované druhy stromov
│   └── TreeGenerator.cs          # Hlavný MonoBehaviour komponent
└── README.md
```

---

### Inštalácia (krok za krokom)

1. **Vytvorte nový Unity projekt** (Unity 2021.3 LTS alebo novší)

2. **Skopírujte priečinok `Scripts/`** do vášho Unity projektu:
   ```
   Assets/ProceduralTree/Scripts/
   ```

3. **Vytvorte scénu:**
   - Vytvorte prázdny `GameObject` v scéne
   - Pomenujte ho "Tree"
   - Pridajte komponent `TreeGenerator` (Add Component → Procedural Tree → Tree Generator)

4. **Vytvorte TreePreset:**
   - V Project okne: pravý klik → Create → Procedural Tree → Tree Preset
   - Pomenujte preset (napr. "Oak_Preset")
   - Priraďte preset do TreeGenerator komponentu

5. **Generujte strom:**
   - V inspektore kliknite tlačidlo **"Generovať strom"**
   - Upravte parametre a kliknite znova pre rôzne tvary

---

### Podporované L-systém symboly

| Symbol | Akcia |
|--------|-------|
| `F`    | Posuň dopredu a nakresli segment vetvy |
| `f`    | Posuň dopredu bez kreslenia |
| `+`    | Otočenie vľavo (yaw) |
| `-`    | Otočenie vpravo (yaw) |
| `&`    | Náklon nadol (pitch) |
| `^`    | Náklon nahor (pitch) |
| `\`    | Rotácia vľavo (roll) |
| `/`    | Rotácia vpravo (roll) |
| `[`    | Uloženie stavu (začiatok vetvy) |
| `]`    | Obnovenie stavu (koniec vetvy) |

---

### Predefinované druhy stromov

Použitie v kóde:
```csharp
TreeSpecies.ApplySpecies(preset, TreeSpeciesType.Oak);
```

| Druh | Opis |
|------|------|
| `SimpleTree` | Jednoduchý strom pre testovanie |
| `Oak` | Dub — široká koruna, nepravidelné vetvenie |
| `Pine` | Borovica — kužeľovitý tvar, pravidelné poschodia |
| `Willow` | Vŕba — prevísajúce vetvy |
| `Birch` | Breza — štíhla, jemné vetvenie |
| `Bush` | Ker — nízky, široký |
| `Palm` | Palma — rovný kmeň, listy na vrchu |

---

### Architektúra systému

```
TreePreset (dáta)
    ↓
LSystem (prepísanie reťazca)
    ↓
TurtleInterpreter (reťazec → kostra stromu)
    ↓
TreeSkeleton (dátová štruktúra)
    ↓
MeshBuilder (kostra → Unity Mesh)
    ↓
TreeGenerator (priradenie do scény)
```

---

### Kľúčové parametre

**Kmeň:**
- `initialLength` — počiatočná dĺžka segmentu
- `initialRadius` — počiatočný polomer kmeňa
- `lengthReduction` — zmenšenie dĺžky pri vetvení
- `radiusReduction` — zmenšenie polomeru pri vetvení

**Vetvenie:**
- `branchAngle` — základný uhol vetvenia
- `angleVariation` — náhodná variácia uhla
- `iterations` — počet L-systém iterácií (viac = detailnejší strom)

**Fyzika:**
- `gravity` — sila gravitácie na vetvy
- `phototropism` — tendencia rásť smerom nahor

**Kvalita:**
- `trunkRadialSegments` — počet segmentov pre kmeň
- `heightSegments` — plynulosť zakrivenia vetiev

---

### Ďalší vývoj (možné rozšírenia pre prácu)

- [ ] Priestorová kolonizácia (space colonization) ako alternatívny algoritmus
- [ ] LOD systém s Unity LODGroup
- [ ] Animácia vetra pomocou vertex shaderu
- [ ] Textúrovanie kôry a listov
- [ ] Export do OBJ/FBX formátu
- [ ] Porovnanie kvality L-systémov vs. space colonization
- [ ] Nekruhové prierezy vetiev (inšpirované Li et al.)
