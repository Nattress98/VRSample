using Unity.Netcode;

internal interface INetworkUser
{
    [ClientRpc]
    void OnSpawnClientRpc(ulong ownerId);
}