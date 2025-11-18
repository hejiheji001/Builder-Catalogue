# Builder Catalogue Challenge

## TL;DR

This project uses **.NET 10** and **Aspire 13** to call the provided Builder Catalogue APIs and answer four questions:

1. Which sets can a given user (e.g. `brickfan35`) build with their current inventory?
2. Who can collaborate with `landscape-artist` to build `tropical-island`?
3. What piece limits should `megabuilder99` use so that at least 50% of users can build their custom model?
4. Which extra sets can `dr_crocodile` build if we allow whole-colour substitutions?

The core logic is implemented in a small domain layer (piece dictionaries and comparison functions), exposed via a simple HTTP API hosted by Aspire.

---

## Problem overview

### 1. Buildable sets for `brickfan35`

- Fetch the user’s inventory and all set definitions from the catalogue APIs.  
- For each set, aggregate requirements by `(designId, colourId)` and compare against the user’s inventory.  
- A set is **buildable** if the user has at least the required quantity for every piece key.

### 2. Collaboration for `landscape-artist` on `tropical-island`

- Load `tropical-island`’s requirements and `landscape-artist`’s inventory.  
- For each other user, combine inventories (`A + B`) and re-run the same buildability check.  
- Return users where the combined inventory can fully satisfy the set.

### 3. 50% coverage limits for `megabuilder99`

- Look at all users’ inventories (excluding `megabuilder99`).  
- For each piece that `megabuilder99` owns, compute how many other users own it (and optionally its median quantity).  
- Keep pieces that are present in at least ~50% of users and cap the usable quantity by a conservative per-piece limit.

### 4. Colour-flexible sets for `dr_crocodile` (HARD)

- For each set, group pieces by original colour and compute requirements per colour.  
- Check which target colours `dr_crocodile` can use to recolour a whole original colour while still having enough pieces.  
- Treat this as a small matching / backtracking problem: each original colour must map to a distinct target colour; if a complete mapping exists, the set becomes buildable with some colour scheme.

---

## Project requirements

- **.NET 10 SDK**  
- **Aspire 13** workloads installed  
- Network access to the provided Builder Catalogue API domain  
- `appsettings.json` or environment variable for the external API base URL, e.g.:

  ```jsonc
  {
    "CatalogueApi": {
      "BaseUrl": "https://<provided-api-domain>"
    }
  }
