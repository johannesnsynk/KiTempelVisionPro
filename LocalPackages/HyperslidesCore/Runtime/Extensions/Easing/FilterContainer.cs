using UnityEngine;

namespace NSYNK
{
    public class FilterContainer
    {
        public FilterContainer(Transform target, IFilter filter)
        {
            this.target = target;
            this.filter = filter;
        }

        public Transform target;
        public IFilter filter;
    }
}