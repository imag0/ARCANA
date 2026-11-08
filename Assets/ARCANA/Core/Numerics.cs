using System;

namespace Arcana.Core
{
    [Serializable]
    public struct D3
    {
        public double x, y, z;
        public D3(double x, double y, double z) { this.x=x; this.y=y; this.z=z; }
        public double LengthSquared => Dot(this,this);
        public double Length => Math.Sqrt(LengthSquared);
        public D3 Unit => Length > 1e-15 ? this/Length : throw new ArgumentException("Zero direction");
        public bool Finite => Numbers.Finite(x)&&Numbers.Finite(y)&&Numbers.Finite(z);
        public static D3 operator +(D3 a,D3 b)=>new D3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static D3 operator -(D3 a,D3 b)=>new D3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static D3 operator -(D3 a)=>a*(-1);
        public static D3 operator *(D3 a,double b)=>new D3(a.x*b,a.y*b,a.z*b);
        public static D3 operator *(double b,D3 a)=>a*b;
        public static D3 operator /(D3 a,double b)=>a*(1/b);
        public static double Dot(D3 a,D3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static D3 Cross(D3 a,D3 b)=>new D3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static double Distance(D3 a,D3 b)=>(a-b).Length;
        public static D3 Lerp(D3 a,D3 b,double t)=>a+(b-a)*t;
        public override string ToString()=>$"({x:G6}, {y:G6}, {z:G6})";
    }
    public static class Numbers
    {
        public static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        public static double Clamp(double v,double lo,double hi)=>Math.Max(lo,Math.Min(hi,v));
    }
    // Independent components (xx, yy, xy, xz, yz); zz=-xx-yy. Off diagonals count twice.
    [Serializable]
    public struct Theta
    {
        public double xx,yy,xy,xz,yz;
        public double zz=>-xx-yy;
        public Theta(double xx,double yy,double xy,double xz,double yz)
        {this.xx=xx;this.yy=yy;this.xy=xy;this.xz=xz;this.yz=yz;}
        public static Theta Q(D3 t) {t=t.Unit;return new Theta(t.x*t.x-1d/3,t.y*t.y-1d/3,t.x*t.y,t.x*t.z,t.y*t.z);}
        public static Theta Source(D3 gradient,double g,double c)=>gradient.LengthSquared<1e-30?default:Q(gradient)*(g*c*gradient.LengthSquared);
        public static double Dot(Theta a,Theta b)=>a.xx*b.xx+a.yy*b.yy+a.zz*b.zz+2*(a.xy*b.xy+a.xz*b.xz+a.yz*b.yz);
        public double Norm=>Math.Sqrt(Math.Max(0,Dot(this,this)));
        public double Contract(D3 n)=>xx*n.x*n.x+yy*n.y*n.y+zz*n.z*n.z+2*(xy*n.x*n.y+xz*n.x*n.z+yz*n.y*n.z);
        public double[,] Matrix()=>new double[,]{{xx,xy,xz},{xy,yy,yz},{xz,yz,zz}};
        public static Theta operator +(Theta a,Theta b)=>new Theta(a.xx+b.xx,a.yy+b.yy,a.xy+b.xy,a.xz+b.xz,a.yz+b.yz);
        public static Theta operator -(Theta a,Theta b)=>a+b*(-1);
        public static Theta operator *(Theta a,double v)=>new Theta(a.xx*v,a.yy*v,a.xy*v,a.xz*v,a.yz*v);
        public double Potential(Model p) {var m=Matrix();double cubic=0;for(int i=0;i<3;i++)for(int j=0;j<3;j++)for(int k=0;k<3;k++)cubic+=m[i,j]*m[j,k]*m[k,i];var n=Dot(this,this);return p.a*n/2-p.b*cubic/3+p.d*n*n/4;}
        // Jacobi diagonalization of a real symmetric matrix, followed by scalar exponentials.
        public double[,] Metric(double g,bool inverse=false)
        {
            var a=Matrix();var v=new double[,]{{1,0,0},{0,1,0},{0,0,1}};
            for(int sweep=0;sweep<48;sweep++)
            {
                int p=0,q=1;double largest=Math.Abs(a[0,1]);
                if(Math.Abs(a[0,2])>largest){p=0;q=2;largest=Math.Abs(a[0,2]);}
                if(Math.Abs(a[1,2])>largest){p=1;q=2;largest=Math.Abs(a[1,2]);}
                if(largest<1e-16*Math.Max(1,Norm))break;
                double angle=.5*Math.Atan2(2*a[p,q],a[q,q]-a[p,p]);
                double c=Math.Cos(angle),s=Math.Sin(angle),pp=a[p,p],qq=a[q,q],pq=a[p,q];
                for(int k=0;k<3;k++)if(k!=p&&k!=q){double kp=a[k,p],kq=a[k,q];a[k,p]=a[p,k]=c*kp-s*kq;a[k,q]=a[q,k]=s*kp+c*kq;}
                a[p,p]=c*c*pp-2*s*c*pq+s*s*qq;a[q,q]=s*s*pp+2*s*c*pq+c*c*qq;a[p,q]=a[q,p]=0;
                for(int k=0;k<3;k++){double vp=v[k,p],vq=v[k,q];v[k,p]=c*vp-s*vq;v[k,q]=s*vp+c*vq;}
            }
            var result=new double[3,3];double factor=(inverse?-2:2)*g/Math.Sqrt(1+Dot(this,this));
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)for(int k=0;k<3;k++)result[i,j]+=v[i,k]*Math.Exp(factor*a[k,k])*v[j,k];
            return result;
        }
    }
    [Serializable]
    public sealed class Model
    {
        public double I=1,K=1,a=1,b=5,d=1,g=1;
        public double Ell=>Math.Sqrt(K/a);
        public void Validate(){if(!Numbers.Finite(I+K+a+b+d+g)||I<=0||K<=0||a<=0||b<=0||d<=0||b*b<=24*a*d||b*b>=27*a*d)throw new ArgumentException("Require I,K,a,b,d > 0 and 24ad < b² < 27ad");}
        public double Green(double r){if(r<=0)throw new ArgumentOutOfRangeException(nameof(r),"Use section integration at the singularity");return Math.Exp(-r/Ell)/(4*Math.PI*K*r);}
    }
    public static class Gauss
    {
        public static void Rule(int n,double lo,double hi,out double[] x,out double[] w)
        {
            x=new double[n];w=new double[n];
            for(int i=0;i<(n+1)/2;i++)
            {
                double z=Math.Cos(Math.PI*(i+.75)/(n+.5)),derivative=0;
                for(int step=0;step<32;step++){double p=1,q=0;for(int j=1;j<=n;j++){double old=p;p=((2*j-1)*z*p-(j-1)*q)/j;q=old;}derivative=n*(z*p-q)/(z*z-1);double next=z-p/derivative;if(Math.Abs(next-z)<2e-15){z=next;break;}z=next;}
                x[i]=(lo+hi)/2-(hi-lo)*z/2;x[n-1-i]=(lo+hi)/2+(hi-lo)*z/2;w[i]=w[n-1-i]=(hi-lo)/((1-z*z)*derivative*derivative);
            }
        }
    }
}
