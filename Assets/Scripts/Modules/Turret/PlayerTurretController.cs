using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerTurretController : MonoBehaviour
{
    [SerializeField] private MonoBehaviour intentSourceComponent;

    private ICharacterIntentSource intentSource;
    private TurretModule activeTurret;

    public bool IsControllingTurret => activeTurret != null;
    public TurretModule ActiveTurret => activeTurret;

    private void Awake()
    {
        ResolveIntentSource();
    }

    private void Update()
    {
        if (activeTurret == null)
            return;

        if (intentSource == null)
            ResolveIntentSource();

        if (intentSource == null)
            return;

        CharacterIntent intent = intentSource.Current;

        activeTurret.AimAtWorldPoint(intent.AimWorldPoint);

        if (intent.PrimaryUseHeld)
            activeTurret.TryFire();

        if (intent.CancelHeld)
            ExitTurretControl();
    }

    public void EnterTurretControl(TurretModule turret)
    {
        if (turret == null)
            return;

        activeTurret = turret;
    }

    public void ExitTurretControl()
    {
        activeTurret = null;
    }

    private void ResolveIntentSource()
    {
        intentSource = intentSourceComponent as ICharacterIntentSource;

        if (intentSource != null)
            return;

        foreach (MonoBehaviour mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is ICharacterIntentSource source)
            {
                intentSource = source;
                intentSourceComponent = mb;
                return;
            }
        }
    }
}