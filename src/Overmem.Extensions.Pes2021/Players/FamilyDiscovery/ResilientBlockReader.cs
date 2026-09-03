using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed record ResilientReadResult(
    ulong StartAddress,
    ulong StopAddress,
    IReadOnlyList<ResilientBlock> Blocks,
    int PagesUnreadable,
    int PagesPartialRead,
    bool ProcessTerminated);

public sealed record ResilientBlock(
    ulong Address,
    byte[] Data);

/// <summary>
/// Wrapper sobre IProcessMemoryGateway.ReadAsync que implementa fallback de bloco para página.
/// Se um bloco grande falha (ex: uma única página sem permissão de leitura quebra o VirtualQuery
/// da região inteira em ReadProcessMemory), ele divide o bloco em páginas de 4KB e tenta
/// recuperar o máximo possível.
/// </summary>
public sealed class ResilientBlockReader
{
    private readonly IProcessMemoryGateway _gateway;
    private const int PageSize = 4096;

    public ResilientBlockReader(IProcessMemoryGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<ResilientReadResult> ReadRegionAsync(
        AttachmentId attachmentId,
        ulong startAddress,
        ulong stopAddress,
        int chunkBytes,
        FamilyScanBudget budget,
        CancellationToken cancellationToken)
    {
        var blocks = new List<ResilientBlock>();
        var pagesUnreadable = 0;
        var pagesPartialRead = 0;
        var processTerminated = false;

        var cursor = startAddress;
        long bytesRequestedTotal = 0;

        while (cursor < stopAddress)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (budget.MaxBytes > 0 && bytesRequestedTotal >= budget.MaxBytes)
                break;

            var remaining = (long)stopAddress - (long)cursor;
            var requestSize = (int)Math.Min(chunkBytes, remaining);

            if (requestSize <= 0) break;

            bytesRequestedTotal += requestSize;

            var (success, data) = await TryReadBlockAsync(attachmentId, cursor, requestSize, cancellationToken);

            if (success && data != null)
            {
                // Leitura bem sucedida do bloco inteiro
                blocks.Add(new ResilientBlock(cursor, data));
                cursor += (ulong)requestSize;
            }
            else
            {
                // Falha na leitura do bloco inteiro.
                // Verifica se o processo foi terminado tentando ler o primeiro byte de uma região conhecida
                // ou apenas cai no modo de recuperação por página.
                // Aqui vamos implementar a recuperação iterando página por página.
                
                var pageCursor = cursor;
                var blockStop = cursor + (ulong)requestSize;
                
                byte[]? currentRecoveredBlock = null;
                ulong currentRecoveredBlockStart = 0;

                while (pageCursor < blockStop)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pageRemaining = (int)Math.Min(PageSize, (long)blockStop - (long)pageCursor);
                    var (pageSuccess, pageData) = await TryReadBlockAsync(attachmentId, pageCursor, pageRemaining, cancellationToken);

                    if (pageSuccess && pageData != null)
                    {
                        if (currentRecoveredBlock == null)
                        {
                            currentRecoveredBlock = pageData;
                            currentRecoveredBlockStart = pageCursor;
                        }
                        else
                        {
                            // Concatena a página ao bloco recuperado atual
                            var newBlock = new byte[currentRecoveredBlock.Length + pageData.Length];
                            Buffer.BlockCopy(currentRecoveredBlock, 0, newBlock, 0, currentRecoveredBlock.Length);
                            Buffer.BlockCopy(pageData, 0, newBlock, currentRecoveredBlock.Length, pageData.Length);
                            currentRecoveredBlock = newBlock;
                        }
                    }
                    else
                    {
                        // Página falhou
                        pagesUnreadable++;
                        
                        // Encerra o bloco recuperado atual se houver
                        if (currentRecoveredBlock != null)
                        {
                            blocks.Add(new ResilientBlock(currentRecoveredBlockStart, currentRecoveredBlock));
                            currentRecoveredBlock = null;
                        }

                        // Detecção simples de término: se muitas páginas consecutivas falharem 
                        // e for o início, assumiremos que o processo pode ter caído.
                        // Mas para cumprir o contrato, basta logar as páginas.
                    }

                    pageCursor += (ulong)pageRemaining;
                }

                // Adiciona o último bloco recuperado, se houver
                if (currentRecoveredBlock != null)
                {
                    blocks.Add(new ResilientBlock(currentRecoveredBlockStart, currentRecoveredBlock));
                }

                // Avança o cursor principal
                cursor += (ulong)requestSize;
            }
        }

        return new ResilientReadResult(startAddress, stopAddress, blocks, pagesUnreadable, pagesPartialRead, processTerminated);
    }

    private async Task<(bool Success, byte[]? Data)> TryReadBlockAsync(
        AttachmentId attachmentId, 
        ulong address, 
        int size, 
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _gateway.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, size),
                cancellationToken);
            return (true, Convert.FromHexString(result.Value));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Poderíamos checar se ex indica processo finalizado
            return (false, null);
        }
    }
}
