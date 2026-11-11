# Rally25rs.EFCoreLazyLoad

An extension for Entity Framework Core that lets you **optionally lazy-load entity relations**, and helps you **find places where a relation was accessed without being loaded**.

## Why

EF Core leaves a navigation property as `null` (or empty) unless you explicitly load it with `Include`. That is good for performance and predictability, but it makes it easy to forget an `Include` and end up with missing data, or to be surprised by lazy loading in code that should have loaded everything up front.

This package gives you control over that behavior:

- **Optional lazy loading** — opt in to loading a relation on demand when it is accessed and has not been loaded yet.
- **Detection** — find out when code needed a relation that was not loaded, and either **log** it or **throw an error**, so missing `Include`s are caught instead of silently returning incomplete data.

## Requirements

- .NET 10
- Entity Framework Core 10

## Status

This package is under active development and is not yet published. Installation and usage instructions will be added here as the API takes shape.
