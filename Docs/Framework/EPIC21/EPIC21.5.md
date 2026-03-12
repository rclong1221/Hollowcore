# EPIC 21.5: Package Metadata & Branding

**Status**: 🔲 NOT STARTED  
**Priority**: MEDIUM  
**Estimated Effort**: 1 day  
**Dependencies**: None

---

## Goal

Update package metadata to be professional and ready for Asset Store submission.

---

## Current Issues

```json
{
  "name": "com.dig.voxel",           // ← Should be company name
  "displayName": "DIG Voxel Engine", // ← Generic name needed
  "author": {
    "name": "DIG Team",              // ← Company name needed
    "url": "https://www.dig.com"     // ← Real URL needed
  }
}
```

---

## Tasks

### Phase 1: Package Identity
- [ ] Decide on final package name (e.g., `com.yourcompany.voxel-engine`)
- [ ] Choose display name (e.g., "DOTS Voxel Engine Pro")
- [ ] Write compelling description (50-100 words)
- [ ] Add relevant keywords

### Phase 2: Author Info
- [ ] Set company/author name
- [ ] Set support email
- [ ] Set website URL
- [ ] Add license file

### Phase 3: Version & Compatibility
- [ ] Set version to 1.0.0 (or appropriate)
- [ ] Verify Unity version compatibility
- [ ] Update dependency versions

### Phase 4: Assets
- [ ] Create package icon (128x128)
- [ ] Create Asset Store banner (if applicable)
- [ ] Screenshots for store page

---

## Final package.json Template

```json
{
  "name": "com.yourcompany.voxel-engine",
  "version": "1.0.0",
  "displayName": "DOTS Voxel Engine",
  "description": "High-performance DOTS-native voxel engine with Marching Cubes meshing, LOD, and optional multiplayer.",
  "unity": "2022.3",
  "keywords": ["voxel", "dots", "ecs", "terrain", "marching-cubes", "procedural"],
  "author": {
    "name": "Your Company",
    "email": "support@yourcompany.com",
    "url": "https://yourcompany.com"
  },
  "license": "SEE LICENSE IN LICENSE.md"
}
```

---

## Success Criteria

- [ ] Professional package name
- [ ] Accurate description
- [ ] Valid author info
- [ ] LICENSE.md file present
- [ ] Package icon created
