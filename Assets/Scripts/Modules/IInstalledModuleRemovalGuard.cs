public interface IInstalledModuleRemovalGuard
{
    bool CanRemoveInstalledModule(out string reason);
}
