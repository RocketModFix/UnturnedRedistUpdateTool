# UnturnedRedistUpdateTool

This is a tool which is mainly intended for the [RocketModFix Unturned Redist](https://github.com/RocketModFix/RocketModFix.Unturned.Redist) to be auto-updated, and used via CI/CD pipeline.

## Publicizer

When `-publicize <assembly>` is passed, the tool rewrites that assembly's non-public members to public so plugin authors can compile against them. The engine lives in [`Publicization/`](src/UnturnedRedistUpdateTool/Publicization) and is vendored/adapted from [krafs/Publicizer](https://github.com/krafs/Publicizer) (MIT) — reshaped from an MSBuild task into a `load → rewrite → write` API so we can ship a pre-publicized DLL.

It deliberately **skips `virtual`/`abstract` members** (and compiler-generated members). Publicizing a `protected virtual` member would force every plugin `override` to widen access to `public`, which the compiler rejects with *"cannot change access rights"* — see [RocketModFix.Unturned.Redist#56](https://github.com/RocketModFix/RocketModFix.Unturned.Redist/issues/56). Consumers of the publicized assembly must set `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` for runtime access on Mono.