from pathlib import Path
p=Path('Unity/ARCANA/Assets/ARCANA/Runtime/Elemental/FluidSurface.cs');s=p.read_text().replace('Vector3 meshCenter;Quaternion meshRotation;', 'readonly float[] kernelX=new float[192*N],kernelY=new float[192*N],kernelZ=new float[192*N],qX=new float[192*N],qY=new float[192*N],qZ=new float[192*N];\n        Vector3 meshCenter;Quaternion meshRotation;');s=s.replace('var step=bounds.size/(N-1);float smooth=Mathf.Min(step.x,Mathf.Min(step.y,step.z))*.8f;', '''var step=bounds.size/(N-1);bool isotropic=half.All(h=>Mathf.Abs(h.x-h.y)<1e-6f&&Mathf.Abs(h.x-h.z)<1e-6f);
            if(isotropic)for(int i=0;i<cells.Length;i++)for(int t=0;t<N;t++)
            {
                int k=i*N+t;float scale=1/(half[i].x*half[i].x);var d=bounds.min+step*t-centers[i];qX[k]=d.x*d.x*scale;qY[k]=d.y*d.y*scale;qZ[k]=d.z*d.z*scale;kernelX[k]=Mathf.Exp(-qX[k]*.35f);kernelY[k]=Mathf.Exp(-qY[k]*.35f);kernelZ[k]=Mathf.Exp(-qZ[k]*.35f);
            }''');old='''var local=rotations[i]*(p-centers[i]);var h=half[i];float q=local.x*local.x/(h.x*h.x)+local.y*local.y/(h.y*h.y)+local.z*local.z/(h.z*h.z);
                    if(q<25)density+=Mathf.Exp(-q*.35f);''';new='''if(isotropic){int k=i*N;if(qX[k+x]+qY[k+y]+qZ[k+z]<25)density+=kernelX[k+x]*kernelY[k+y]*kernelZ[k+z];}
                    else {var local=rotations[i]*(p-centers[i]);var h=half[i];float q=local.x*local.x/(h.x*h.x)+local.y*local.y/(h.y*h.y)+local.z*local.z/(h.z*h.z);if(q<25)density+=Mathf.Exp(-q*.35f);}''';assert old in s;s=s.replace(old,new);p.write_text(s)
