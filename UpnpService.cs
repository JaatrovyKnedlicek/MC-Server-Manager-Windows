using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Open.Nat;

namespace MC_Server_Manager_3
{
    public class UpnpService
    {
        private static readonly Lazy<UpnpService> instance = new(() => new UpnpService());
        public static UpnpService Instance => instance.Value;

        private readonly record struct PortMappingKey(int Port, Protocol Protocol);
        
        private readonly Dictionary<PortMappingKey, NatDevice> mappedPorts = new();
        private readonly SemaphoreSlim semaphore = new(1, 1);
        private NatDiscoverer? discoverer;

        private UpnpService() { }

        public async Task<bool> OpenPortAsync(int port, string description, CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                discoverer ??= new NatDiscoverer();
                
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout for discovery

                try
                {
                    var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
                    
                    // Open TCP port
                    var tcpKey = new PortMappingKey(port, Protocol.Tcp);
                    if (!mappedPorts.ContainsKey(tcpKey))
                    {
                        await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, description + " (TCP)"));
                        mappedPorts[tcpKey] = device;
                    }
                    
                    // Open UDP port
                    var udpKey = new PortMappingKey(port, Protocol.Udp);
                    if (!mappedPorts.ContainsKey(udpKey))
                    {
                        await device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, description + " (UDP)"));
                        mappedPorts[udpKey] = device;
                    }
                    
                    return true;
                }
                catch (OperationCanceledException)
                {
                    // Discovery timed out - likely no UPnP device available
                    return false;
                }
                catch (Exception)
                {
                    // Other UPnP errors
                    return false;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task ClosePortAsync(int port, CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Close TCP port
                var tcpKey = new PortMappingKey(port, Protocol.Tcp);
                if (mappedPorts.TryGetValue(tcpKey, out var device))
                {
                    try
                    {
                        await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port));
                    }
                    catch
                    {
                        // Ignore errors when closing port
                    }
                    finally
                    {
                        mappedPorts.Remove(tcpKey);
                    }
                }
                
                // Close UDP port
                var udpKey = new PortMappingKey(port, Protocol.Udp);
                if (mappedPorts.TryGetValue(udpKey, out var udpDevice))
                {
                    try
                    {
                        await udpDevice.DeletePortMapAsync(new Mapping(Protocol.Udp, port, port));
                    }
                    catch
                    {
                        // Ignore errors when closing port
                    }
                    finally
                    {
                        mappedPorts.Remove(udpKey);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task CloseAllPortsAsync(CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var mappings = new List<PortMappingKey>(mappedPorts.Keys);
                foreach (var mappingKey in mappings)
                {
                    if (mappedPorts.TryGetValue(mappingKey, out var device))
                    {
                        try
                        {
                            await device.DeletePortMapAsync(new Mapping(mappingKey.Protocol, mappingKey.Port, mappingKey.Port));
                        }
                        catch
                        {
                            // Ignore errors when closing port
                        }
                        finally
                        {
                            mappedPorts.Remove(mappingKey);
                        }
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public bool IsPortMapped(int port)
        {
            return mappedPorts.ContainsKey(new PortMappingKey(port, Protocol.Tcp)) || 
                   mappedPorts.ContainsKey(new PortMappingKey(port, Protocol.Udp));
        }
    }
}