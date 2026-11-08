using System;
using System.Collections.Generic;
using System.Threading;
using Arcana.Core;

namespace Arcana.Field
{
    public readonly struct Sample
    {
        public readonly D3 p;public readonly double volume;
        public Sample(D3 p,double volume){this.p=p;this.volume=volume;}
    }
    public sealed class Cylinder
    {
        public readonly D3 a,b,axis,center;public readonly double radius,length,C,strain;public readonly Theta J;
        public readonly Sample[] coarse,fine;
        public Cylinder(D3 a,D3 b,double radius,double C,double strain,Model model)
        {
            if(radius<=0||C<=0||!Numbers.Finite(radius+C+strain)||!a.Finite||!b.Finite)throw new ArgumentException("Invalid cylinder");
            this.a=a;this.b=b;this.radius=radius;this.C=C;this.strain=strain;axis=(b-a).Unit;center=(a+b)/2;length=D3.Distance(a,b);J=Theta.Q(axis)*(model.g*C*strain*strain);
            coarse=VolumeRule(8,3,12);fine=VolumeRule(16,6,24);
        }
        public double Volume=>Math.PI*radius*radius*length;
        public double Energy=>C*strain*strain*Volume/2;
        public Sample[] VolumeRule(int ns,int nr,int na)
        {
            D3 refAxis=Math.Abs(axis.x)<.7?new D3(1,0,0):new D3(0,1,0);D3 u=D3.Cross(axis,refAxis).Unit,v=D3.Cross(axis,u);
            Gauss.Rule(ns,-length/2,length/2,out var s,out var ws);Gauss.Rule(nr,0,radius,out var r,out var wr);
            var samples=new Sample[ns*nr*na];int index=0;
            for(int i=0;i<ns;i++)for(int j=0;j<nr;j++)for(int k=0;k<na;k++){double phi=2*Math.PI*k/na;samples[index++]=new Sample(center+axis*s[i]+(u*Math.Cos(phi)+v*Math.Sin(phi))*r[j],ws[i]*wr[j]*r[j]*2*Math.PI/na);}
            return samples;
        }
        public double DistanceToAxis(D3 x){double t=Numbers.Clamp(D3.Dot(x-a,axis),0,length);return D3.Distance(x,a+axis*t);}
        public bool Overlaps(D3 p,double padding){double t=D3.Dot(p-a,axis);return t>=-padding&&t<=length+padding&&(p-a-axis*t).Length<=radius+padding;}
    }
    public readonly struct FieldValue
    {
        public readonly Theta value;public readonly double error;public readonly string route;
        public FieldValue(Theta value,double error,string route){this.value=value;this.error=error;this.route=route;}
    }
    public sealed class SourceCache
    {
        public readonly Cylinder[] sources;public readonly Model model;public readonly double normBound;
        public SourceCache(IEnumerable<Cylinder> sources,Model model)
        {
            model.Validate();this.model=model;this.sources=new List<Cylinder>(sources).ToArray();
            // Rearrangement bound: radial decreasing Yukawa kernel integrated over equal-volume ball.
            foreach(var s in this.sources){double r=Math.Pow(3*s.Volume/(4*Math.PI),1d/3),q=r/model.Ell;normBound+=s.J.Norm/model.a*(1-(1+q)*Math.Exp(-q));}
        }
        public FieldValue At(D3 x,CancellationToken cancel=default,bool allowLine=true)
        {
            Theta sum=default;double error=0;string route="finite cylinder";
            foreach(var s in sources)
            {
                cancel.ThrowIfCancellationRequested();if(s.J.Norm==0)continue;
                double low,high;
                if(s.DistanceToAxis(x)<2.5*s.radius)
                {
                    // Observation-centred spherical coordinates integrate the radial singularity analytically.
                    // Valid for points inside, on or outside the finite cylinder. No epsilon-clamped kernel.
                    low=RadialIntegral(s,x,model,32);high=RadialIntegral(s,x,model,64);route="section / analytic radial kernel";
                }
                else
                {
                    low=Quadrature(s.coarse,x,model);high=Quadrature(s.fine,x,model);
                    if(allowLine&&s.DistanceToAxis(x)>=5*s.radius)
                    {
                        double line=LineIntegral(s,x,model,24);
                        // Only accept accelerated line value when checked against actual finite section.
                        if(Math.Abs(line-high)*s.J.Norm<1e-11){low=high;high=line;route="line (finite-section checked)";}
                    }
                }
                sum+=s.J*high;error+=s.J.Norm*Math.Abs(high-low)*2;
            }
            return new FieldValue(sum,error,route);
        }
        static double Quadrature(Sample[] samples,D3 x,Model p)
        {double sum=0;foreach(var s in samples)sum+=s.volume*p.Green(D3.Distance(s.p,x));return sum;}
        public static double LineIntegral(Cylinder s,D3 x,Model p,int n)
        {Gauss.Rule(n,0,s.length,out var t,out var w);double sum=0;for(int i=0;i<n;i++)sum+=w[i]*p.Green(D3.Distance(x,s.a+s.axis*t[i]));return sum*Math.PI*s.radius*s.radius;}
        // dV=r² dr dOmega cancels 1/r. The primitive of r exp(-r/ell) is -ell(r+ell)exp(-r/ell).
        public static double RadialIntegral(Cylinder s,D3 x,Model p,int n)
        {
            Gauss.Rule(n,-1,1,out var mu,out var weights);int nPhi=2*n;double sum=0;
            for(int i=0;i<n;i++)for(int j=0;j<nPhi;j++)
            {
                double phi=(j+.5)*2*Math.PI/nPhi,sin=Math.Sqrt(1-mu[i]*mu[i]);var ray=new D3(sin*Math.Cos(phi),sin*Math.Sin(phi),mu[i]);
                if(!RayInterval(s,x,ray,out double lo,out double hi))continue;
                double ell=p.Ell;double radial=ell*((lo+ell)*Math.Exp(-lo/ell)-(hi+ell)*Math.Exp(-hi/ell));
                sum+=weights[i]*2*Math.PI/nPhi*radial/(4*Math.PI*p.K);
            }
            return sum;
        }
        static bool RayInterval(Cylinder s,D3 origin,D3 ray,out double lo,out double hi)
        {
            var offset=origin-s.a;double h=D3.Dot(offset,s.axis),dh=D3.Dot(ray,s.axis);lo=0;hi=double.PositiveInfinity;
            if(Math.Abs(dh)<1e-14){if(h<0||h>s.length)return false;}
            else{double x=-h/dh,y=(s.length-h)/dh;lo=Math.Max(lo,Math.Min(x,y));hi=Math.Min(hi,Math.Max(x,y));}
            var radial=offset-s.axis*h;var d=ray-s.axis*dh;double aa=d.LengthSquared,bb=2*D3.Dot(radial,d),cc=radial.LengthSquared-s.radius*s.radius;
            if(aa<1e-20){if(cc>0)return false;}
            else{double disc=bb*bb-4*aa*cc;if(disc<0)return false;double root=Math.Sqrt(disc);lo=Math.Max(lo,(-bb-root)/(2*aa));hi=Math.Min(hi,(-bb+root)/(2*aa));}
            return hi>lo&&Numbers.Finite(hi);
        }
        public Measurement Measure(D3 center,D3 axis,double length=.05,double radius=.002,CancellationToken cancel=default)
        {
            axis=axis.Unit;Gauss.Rule(20,-length/2,length/2,out var s,out var weights);double value=0,error=0,norm=0;
            var middle=At(center,cancel);string route=middle.route;
            for(int i=0;i<s.Length;i++)
            {
                var point=center+axis*s[i];foreach(var source in sources)if(source.Overlaps(point,radius))return new Measurement{available=false,status="Probe intersects source: independent-probe model unavailable"};
                double weight=weights[i]*Math.Pow(Math.Sin(Math.PI*s[i]/length),2);var field=At(point,cancel);value-=model.g*weight*field.value.Contract(axis);error+=Math.Abs(model.g)*weight*field.error;norm+=weight;
            }
            double absoluteError=error/norm;
            return new Measurement{available=absoluteError<=1e-9,shift=value/norm,error=absoluteError,center=middle.value,route=route,normBound=normBound,status=absoluteError>1e-9?"Quadrature unresolved: reading unavailable":"Measured / weak-static diagnostic"};
        }
        // Finite-volume energy: 1/2 integral J:Theta. Interior integrals use the analytic radial treatment.
        // Limited to compact diagnostics; result carries an a posteriori refinement difference.
        public EnergyEstimate FieldEnergy(CancellationToken cancel=default)
        {
            if(sources.Length>8)return new EnergyEstimate{available=false,reason="Energy quadrature budget: more than 8 source cylinders"};
            double Integrate(int outer,int angular,out double self)
            {
                double sum=0;self=0;
                foreach(var s in sources)foreach(var q in s.VolumeRule(outer,2,8))
                {
                    cancel.ThrowIfCancellationRequested();Theta value=default;
                    foreach(var t in sources){double kernel=t.DistanceToAxis(q.p)<2.5*t.radius?RadialIntegral(t,q.p,model,angular):Quadrature(t.fine,q.p,model);value+=t.J*kernel;if(ReferenceEquals(t,s))self+=.5*q.volume*Theta.Dot(s.J,t.J)*kernel;}
                    sum+=.5*q.volume*Theta.Dot(s.J,value);
                }
                return sum;
            }
            double low=Integrate(4,16,out _),high=Integrate(6,32,out double selfEnergy);double error=2*Math.Abs(high-low);
            return new EnergyEstimate{available=Numbers.Finite(high)&&error<=Math.Max(1e-13,Math.Abs(high)*.02),field=high,self=selfEnergy,cross=high-selfEnergy,error=error,reason="finite volume; 2 x refinement difference (estimate, not certified bound)"};
        }
    }
    public sealed class Measurement {public bool available;public double shift,error,normBound;public Theta center;public string status,route;public bool Resolved=>available&&Math.Abs(shift)>=1e-8;}
    public sealed class EnergyEstimate {public bool available;public double field,self,cross,error;public string reason;}
    public static class WFixture
    {
        public static Cylinder Source(double strain=.02)=>new Cylinder(new D3(-.2,0,0),new D3(.2,0,0),.03,50,strain,new Model());
        public static Measurement Evaluate(double strain=.02)=>new SourceCache(new[]{Source(strain)},new Model()).Measure(new D3(0,.5,0),new D3(1,0,0));
    }
}
