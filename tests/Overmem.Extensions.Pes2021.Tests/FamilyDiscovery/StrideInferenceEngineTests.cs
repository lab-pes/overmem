using System;
using System.Collections.Generic;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;
using Xunit;

namespace Overmem.Extensions.Pes2021.Tests.FamilyDiscovery;

public class StrideInferenceEngineTests
{
    private static FamilyHit CreateHit(ulong address)
    {
        return new FamilyHit(address, 1, "test", FamilyResultClass.MaskedRecordCopy, 10, Array.Empty<string>(), true);
    }

    [Fact]
    public void InferStride_TooFewHits_ReturnsIsolated()
    {
        var hits = new[] { CreateHit(1000), CreateHit(1380) };
        var result = StrideInferenceEngine.InferStride(hits, 3);
        
        Assert.Equal(FamilyResultClass.IsolatedHit, result.ResultClass);
        Assert.Null(result.InferredStride);
    }

    [Fact]
    public void InferStride_Finds380Stride()
    {
        var hits = new[]
        {
            CreateHit(1000),
            CreateHit(1380),
            CreateHit(1760),
            CreateHit(2140),
            CreateHit(2520)
        };

        var result = StrideInferenceEngine.InferStride(hits, 3);
        
        Assert.Equal(FamilyResultClass.SameLayoutFamily, result.ResultClass);
        Assert.Equal(380, result.InferredStride);
        Assert.Equal(1000 % 380, result.InferredResidue);
    }

    [Fact]
    public void InferStride_Finds760Stride()
    {
        var hits = new[]
        {
            CreateHit(1000),
            CreateHit(1760),
            CreateHit(2520),
            CreateHit(3280)
        };

        var result = StrideInferenceEngine.InferStride(hits, 3);
        
        Assert.Equal(FamilyResultClass.AlternateStrideFamily, result.ResultClass);
        Assert.Equal(760, result.InferredStride);
    }

    [Fact]
    public void InferStride_AmbiguousTie_ReturnsAmbiguous()
    {
        var hits = new[]
        {
            CreateHit(1000),
            CreateHit(1380),
            CreateHit(1760),
            
            CreateHit(5000),
            CreateHit(5760),
            CreateHit(6520)
        };

        // Aqui temos 3 hits espaçados de 380 e 3 hits espaçados de 760. 
        // Eles estão em "regiões" separadas logicamente, mas a engine de stride as processa juntas
        // O max count vai ser 3 para ambos os strides (um com resíduo 1000%380 e outro com 5000%760).
        // Deve dar empate (AmbiguousFamily).
        
        var result = StrideInferenceEngine.InferStride(hits, 3);
        
        Assert.Equal(FamilyResultClass.AmbiguousFamily, result.ResultClass);
    }
}
