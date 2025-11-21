# Builder Catalogue Challenge

## TL;DR

This project uses **.NET 10** and **Aspire 13** to call the provided Builder Catalogue APIs and answer four questions:

1. Which sets can a given user (e.g. `brickfan35`) build with their current inventory?
2. Who can collaborate with `landscape-artist` to build `tropical-island`?
3. What piece limits should `megabuilder99` use so that at least 50% of users can build their custom model?
4. Which extra sets can `dr_crocodile` build if we allow whole-color substitutions?

The core logic is implemented in a small domain layer (piece dictionaries and comparison functions), exposed via a simple HTTP API hosted by Aspire.

---

## Problem overview

### 1. Buildable sets for `brickfan35`

- Fetch the user’s inventory and all set definitions from the catalogue APIs.  
- For each set, aggregate requirements by `(designId, colorId)` and compare against the user’s inventory.  
- A set is **buildable** if the user has at least the required quantity for every piece key.

### 2. Collaboration for `landscape-artist` on `tropical-island`

- Load `tropical-island`’s requirements and `landscape-artist`’s inventory.  
- For each other user, combine inventories (`A + B`) and re-run the same buildability check.  
- Return users where the combined inventory can fully satisfy the set.

### 3. 50% coverage limits for `megabuilder99`

- Look at all users’ inventories (excluding `megabuilder99`).  
- For each piece that `megabuilder99` owns, compute how many other users own it (and optionally its median quantity).  
- Keep pieces that are present in at least ~50% of users and cap the usable quantity by a conservative per-piece limit.

### 4. Color-flexible sets for `dr_crocodile`

- For each set, group pieces by original color and compute requirements per color.  
- Check which target colors `dr_crocodile` can use to re-color a whole original color while still having enough pieces.  
- Treat this as a small matching / backtracking problem: each original color must map to a distinct target color; if a complete mapping exists, the set becomes buildable with color substitution.

---

## Architecture and assumptions

- **Single-process composition**: Aspire hosts the API and background services in-process. In production we would separate the domain API, cache refreshers, and batch jobs into deployable units to isolate failures and allow independent scaling.
- **Upstream dependency**: We assume the catalogue API is reliable and that schema changes are coordinated. Real deployments should version DTOs, add health checks, and implement retry policies on `ICatalogueApiClient`.
- **Cache**: The system loads a static JSON file at startup and never refreshes. A real cache would run on Redis, we can use `HybridCache` for example in production environment.
- **Synchronous composition**: Controllers call the upstream API synchronously per request. For heavier workloads we would queue long-running insights or precompute aggregates asynchronously to protect tail latency.
- **Security posture**: No authentication, authorization in current codebase. Production builds need API gateway integration, auth policies, logging, and privacy reviews.
