namespace HnVue.Workflow.Protocol;

using System.Threading.Tasks;

/// <summary>
/// Interface for protocol repository operations.
/// SPEC-WORKFLOW-001 FR-WF-08: Protocol selection and validation
/// SPEC-WORKFLOW-001 FR-WF-09: Safety limit enforcement on protocol save
/// </summary>
/// <remarks>
/// @MX:ANCHOR: Protocol repository interface - protocol management contract
/// @MX:REASON: High fan_in from workflow handlers that need protocol access. Defines protocol storage contract.
/// </remarks>
public interface IProtocolRepository
{
    /// <summary>
    /// Gets a protocol by its identifier.
    /// </summary>
    /// <param name="protocolId">The protocol identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The protocol, or null if not found.</returns>
    Task<Protocol?> GetProtocolAsync(System.Guid protocolId, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active protocols.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>All active protocols.</returns>
    Task<Protocol[]> GetAllAsync(System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new protocol with safety limit validation.
    /// SPEC-WORKFLOW-001 FR-WF-09: Enforce safety limits on protocol save
    /// </summary>
    /// <param name="protocol">The protocol to create.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if successful; false if validation failed.</returns>
    Task<bool> CreateAsync(Protocol protocol, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing protocol with safety limit validation.
    /// SPEC-WORKFLOW-001 FR-WF-09: Enforce safety limits on protocol save
    /// </summary>
    /// <param name="protocol">The protocol to update.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if successful; false if validation failed.</returns>
    Task<bool> UpdateAsync(Protocol protocol, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a protocol (soft delete by setting IsActive = false).
    /// </summary>
    /// <param name="protocolId">The protocol identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DeleteAsync(System.Guid protocolId, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets protocols by body part.
    /// </summary>
    /// <param name="bodyPart">The body part to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Matching protocols.</returns>
    Task<Protocol[]> GetProtocolsByBodyPartAsync(string bodyPart, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a protocol by composite key (BodyPart, Projection, DeviceModel).
    /// SPEC-WORKFLOW-001 FR-WF-08: Protocol lookup by composite key
    /// Target: 50ms or better lookup performance for 500+ protocols
    /// </summary>
    /// <param name="bodyPart">The body part.</param>
    /// <param name="projection">The projection/view.</param>
    /// <param name="deviceModel">The device model.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The protocol, or null if not found.</returns>
    Task<Protocol?> GetByCompositeKeyAsync(string bodyPart, string projection, string deviceModel, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets protocols by procedure code.
    /// SPEC-WORKFLOW-001 FR-WF-08: N-to-1 procedure code to protocol mapping
    /// </summary>
    /// <param name="procedureCode">The procedure code.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Matching protocols.</returns>
    Task<Protocol[]> GetByProcedureCodeAsync(string procedureCode, System.Threading.CancellationToken cancellationToken = default);
}
