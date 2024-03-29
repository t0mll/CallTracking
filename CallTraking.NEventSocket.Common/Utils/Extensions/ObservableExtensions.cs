﻿using System;
using System.Reactive.Linq;

namespace CallTraking.NEventSocket.Common.Utils.Extensions
{
    public static class ObservableExtensions
    {
        /// <summary>Aggregates a Stream using the supplied Aggregator until the given predicate is true</summary>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accumulator">The accumulator.</param>
        /// <param name="predicate">A predicate which indicates whether the aggregation is completed.</param>
        /// <typeparam name="TSource">The Type of the Source stream.</typeparam>
        /// <typeparam name="TAccumulate">The Type of the Accumulator.</typeparam>
        /// <returns>The <see cref="IObservable{T}"/>.</returns>
        public static IObservable<TAccumulate> AggregateUntil<TSource, TAccumulate>(
            this IObservable<TSource> source,
            Func<TAccumulate> seed,
            Func<TAccumulate, TSource, TAccumulate> accumulator,
            Func<TAccumulate, bool> predicate)
        {
            return Observable.Create<TAccumulate>(
                observer =>
                {
                    var accumulate = seed();

                    return source.Subscribe(
                        value =>
                        {
                            accumulate = accumulator(accumulate, value);

                            if (predicate(accumulate))
                            {
                                observer.OnNext(accumulate);
                                accumulate = seed();
                            }
                        },
                        observer.OnError,
                        observer.OnCompleted);
                });
        }
    }
}
